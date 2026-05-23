using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Timers;
using DevEnv.Models;

namespace DevEnv.Services
{
    public class ProcessManager
    {
        private readonly AppConfigService _configService;
        private readonly Dictionary<string, ProcessDefinition> _definitions = new();
        private readonly Dictionary<string, int> _trackedPids = new();
        private readonly string _statePath;
        private readonly System.Timers.Timer _timer;

        public event EventHandler<ProcessStatusUpdatedEventArgs>? ProcessStatusUpdated;

        public ProcessManager(AppConfigService configService)
        {
            _configService = configService;
            AppPaths.EnsureDirectories();
            _statePath = AppPaths.ProcessesStateFile;
            LoadDefinitions();
            LoadState();

            _timer = new System.Timers.Timer(2000);
            _timer.Elapsed += CheckAllProcessesStatus;
        }

        public IReadOnlyList<ProcessDefinition> GetDefinitions() => _definitions.Values.ToList();

        public void StartMonitoring() => _timer.Start();

        public void StopMonitoring()
        {
            _timer.Stop();
            _timer.Dispose();
        }

        public ProcessStatusInfo GetProcessStatus(string name)
        {
            if (!_definitions.TryGetValue(name, out var def))
                return new ProcessStatusInfo { Status = ProcessState.Unknown, DisplayText = "未知", Color = "Gray" };

            var exePath = ResolvePath(def.Executable);
            if (!File.Exists(exePath))
                return new ProcessStatusInfo { Status = ProcessState.NotConfigured, DisplayText = "未安装", Color = "Orange" };

            if (_trackedPids.TryGetValue(name, out var pid) && IsProcessAlive(pid))
                return new ProcessStatusInfo { Status = ProcessState.Running, DisplayText = $"运行中 (PID {pid})", Color = "Green", ProcessId = pid };

            _trackedPids.Remove(name);
            SaveState();

            var externalPid = FindExternalProcessId(def);
            if (externalPid.HasValue)
                return new ProcessStatusInfo { Status = ProcessState.ExternalRunning, DisplayText = $"运行中 (PID {externalPid})", Color = "DodgerBlue", ProcessId = externalPid };

            return new ProcessStatusInfo { Status = ProcessState.Stopped, DisplayText = "已停止", Color = "Gray" };
        }

        public Task<(bool Success, string Message)> StartProcessAsync(string name)
        {
            if (!_definitions.TryGetValue(name, out var def))
                return Task.FromResult((false, $"{name} 未定义"));

            var status = GetProcessStatus(name);
            if (status.Status is ProcessState.Running or ProcessState.ExternalRunning)
                return Task.FromResult((true, $"{def.DisplayName} 已在运行中"));

            var exePath = ResolvePath(def.Executable);
            if (!File.Exists(exePath))
                return Task.FromResult((false, $"{def.DisplayName} 未安装，请先从软件下载器下载绿色版并解压到应用目录"));

            try
            {
                var workDir = ResolvePath(def.WorkingDirectory);
                if (!Directory.Exists(workDir))
                    Directory.CreateDirectory(workDir);

                EnsureRuntimeDirectories(name);

                var args = ResolvePath(def.Arguments);
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    WorkingDirectory = workDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(startInfo);
                if (process == null)
                    return Task.FromResult((false, "进程启动失败"));

                _trackedPids[name] = process.Id;
                SaveState();
                return Task.FromResult((true, $"{def.DisplayName} 已启动 (PID {process.Id})"));
            }
            catch (Exception ex)
            {
                return Task.FromResult((false, $"启动失败: {ex.Message}"));
            }
        }

        public Task<(bool Success, string Message)> StopProcessAsync(string name)
        {
            if (!_definitions.TryGetValue(name, out var def))
                return Task.FromResult((false, $"{name} 未定义"));

            try
            {
                var stopped = false;

                if (_trackedPids.TryGetValue(name, out var pid))
                {
                    stopped |= TryKillProcess(pid);
                    _trackedPids.Remove(name);
                }

                foreach (var processName in def.ProcessNames)
                {
                    foreach (var proc in Process.GetProcessesByName(processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (IsProcessFromAppsDir(proc, def))
                            stopped |= TryKillProcess(proc.Id);
                    }
                }

                SaveState();
                return Task.FromResult(stopped
                    ? (true, $"{def.DisplayName} 已停止")
                    : (true, $"{def.DisplayName} 当前未运行"));
            }
            catch (Exception ex)
            {
                return Task.FromResult((false, $"停止失败: {ex.Message}"));
            }
        }

        public string GetAppsDirectory()
        {
            var settings = _configService.Load();
            var appsDir = string.IsNullOrWhiteSpace(settings.AppsDir) ? AppPaths.AppsDir : settings.AppsDir;
            Directory.CreateDirectory(appsDir);
            return appsDir;
        }

        private void CheckAllProcessesStatus(object? sender, ElapsedEventArgs e)
        {
            try
            {
                foreach (var name in _definitions.Keys)
                {
                    var status = GetProcessStatus(name);
                    ProcessStatusUpdated?.Invoke(this, new ProcessStatusUpdatedEventArgs(name, status));
                }
            }
            catch
            {
                // ignore polling errors
            }
        }

        private void LoadDefinitions()
        {
            _definitions.Clear();
            var config = ResourceLoader.LoadBundledJson<Dictionary<string, ProcessConfigEntry>>(
                ResourcePaths.ProcessesConfigFile);
            if (config == null) return;

            foreach (var (key, entry) in config)
            {
                _definitions[key] = new ProcessDefinition
                {
                    Name = key,
                    DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? key : entry.DisplayName,
                    Description = entry.Description,
                    ProcessNames = entry.ProcessNames ?? [],
                    Executable = entry.Executable,
                    Arguments = entry.Arguments,
                    WorkingDirectory = entry.WorkingDirectory
                };
            }
        }

        private void LoadState()
        {
            try
            {
                if (!File.Exists(_statePath)) return;
                var json = File.ReadAllText(_statePath);
                var state = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                if (state == null) return;

                foreach (var (name, pid) in state)
                {
                    if (IsProcessAlive(pid))
                        _trackedPids[name] = pid;
                }
            }
            catch
            {
                // ignore corrupt state
            }
        }

        private void SaveState()
        {
            var alive = _trackedPids.Where(p => IsProcessAlive(p.Value))
                .ToDictionary(p => p.Key, p => p.Value);
            _trackedPids.Clear();
            foreach (var item in alive)
                _trackedPids[item.Key] = item.Value;

            var json = JsonSerializer.Serialize(_trackedPids, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_statePath, json);
        }

        private string ResolvePath(string template)
        {
            if (string.IsNullOrEmpty(template)) return template;
            return template.Replace("{apps_dir}", GetAppsDirectory().Replace('\\', '/'))
                           .Replace('/', Path.DirectorySeparatorChar);
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                return !proc.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private int? FindExternalProcessId(ProcessDefinition def)
        {
            foreach (var processName in def.ProcessNames)
            {
                var name = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                foreach (var proc in Process.GetProcessesByName(name))
                {
                    if (IsProcessFromAppsDir(proc, def))
                        return proc.Id;
                }
            }
            return null;
        }

        private bool IsProcessFromAppsDir(Process proc, ProcessDefinition def)
        {
            try
            {
                var appsDir = GetAppsDirectory();
                var modulePath = proc.MainModule?.FileName;
                return modulePath != null &&
                       modulePath.StartsWith(appsDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return _trackedPids.ContainsValue(proc.Id);
            }
        }

        private static bool TryKillProcess(int pid)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                if (proc.HasExited) return false;
                proc.Kill(true);
                proc.WaitForExit(3000);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void EnsureRuntimeDirectories(string name)
        {
            var appsDir = GetAppsDirectory();
            switch (name)
            {
                case "MongoDB":
                    Directory.CreateDirectory(Path.Combine(appsDir, "mongodb", "data"));
                    break;
                case "MinIO":
                    Directory.CreateDirectory(Path.Combine(appsDir, "minio", "data"));
                    break;
                case "Redis":
                    EnsureRedisConfig(appsDir);
                    break;
            }
        }

        private static void EnsureRedisConfig(string appsDir)
        {
            var redisDir = Path.Combine(appsDir, "redis");
            Directory.CreateDirectory(redisDir);
            var confPath = Path.Combine(redisDir, "redis.windows.conf");
            if (!File.Exists(confPath))
            {
                File.WriteAllText(confPath, "bind 127.0.0.1\r\nport 6379\r\n");
            }
        }
    }
}
