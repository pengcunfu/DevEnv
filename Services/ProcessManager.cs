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
                var javaHome = name == "Kafka" ? ResolveJavaHome(appsDir: GetAppsDirectory()) : null;
                if (name == "Kafka" && javaHome == null)
                    return Task.FromResult((false, "Kafka 需要 Java，请先从软件下载器安装 OpenJDK 绿色版"));

                var startInfo = CreateProcessStartInfo(exePath, args, workDir, javaHome);

                var process = Process.Start(startInfo);
                if (process == null)
                    return Task.FromResult((false, "进程启动失败"));

                var startupDelay = name == "Kafka" ? 5000 : 800;
                Thread.Sleep(startupDelay);
                if (process.HasExited)
                {
                    return Task.FromResult((false,
                        $"{def.DisplayName} 启动后立即退出 (代码 {process.ExitCode})，请检查配置文件或应用目录"));
                }

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
                case "PostgreSQL":
                    EnsurePostgreSQLRuntime(appsDir);
                    break;
                case "Kafka":
                    EnsureKafkaRuntime(appsDir);
                    break;
            }
        }

        private void EnsureKafkaRuntime(string appsDir)
        {
            var kafkaDir = Path.Combine(appsDir, "kafka");
            var configDir = Path.Combine(kafkaDir, "config");
            var dataDir = Path.Combine(kafkaDir, "data");
            var configPath = Path.Combine(configDir, "devenv-server.properties");
            Directory.CreateDirectory(configDir);
            Directory.CreateDirectory(dataDir);

            var configContent =
                "process.roles=broker,controller\r\n" +
                "node.id=1\r\n" +
                "controller.quorum.voters=1@localhost:9093\r\n" +
                "listeners=PLAINTEXT://:9092,CONTROLLER://:9093\r\n" +
                "inter.broker.listener.name=PLAINTEXT\r\n" +
                "advertised.listeners=PLAINTEXT://localhost:9092\r\n" +
                $"log.dirs={dataDir.Replace('\\', '/')}\r\n" +
                "num.partitions=1\r\n" +
                "offsets.topic.replication.factor=1\r\n" +
                "transaction.state.log.replication.factor=1\r\n" +
                "transaction.state.log.min.isr=1\r\n" +
                "controller.listener.names=CONTROLLER\r\n";
            File.WriteAllText(configPath, configContent);

            var metaFile = Path.Combine(dataDir, "meta.properties");
            if (File.Exists(metaFile))
                return;

            var javaHome = ResolveJavaHome(appsDir);
            if (javaHome == null)
                throw new InvalidOperationException("未找到 Java，请先安装 OpenJDK 绿色版");

            var storageBat = Path.Combine(kafkaDir, "bin", "windows", "kafka-storage.bat");
            if (!File.Exists(storageBat))
                throw new FileNotFoundException("未找到 kafka-storage.bat，请确认 Kafka 已正确解压到应用目录");

            var clusterId = RunKafkaCommand(storageBat, "random-uuid", kafkaDir, javaHome).Trim();
            if (string.IsNullOrWhiteSpace(clusterId))
                throw new InvalidOperationException("无法生成 Kafka 集群 ID");

            var formatResult = RunKafkaCommand(
                storageBat,
                $"format -t {clusterId} -c \"{configPath}\"",
                kafkaDir,
                javaHome);

            if (!File.Exists(metaFile))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(formatResult)
                    ? "Kafka 存储初始化失败"
                    : formatResult.Trim());
            }
        }

        private static string? ResolveJavaHome(string appsDir)
        {
            var portableJava = Path.Combine(appsDir, "java");
            if (File.Exists(Path.Combine(portableJava, "bin", "java.exe")))
                return portableJava;

            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrWhiteSpace(javaHome) &&
                File.Exists(Path.Combine(javaHome, "bin", "java.exe")))
                return javaHome;

            return null;
        }

        private static string RunKafkaCommand(string batPath, string arguments, string workingDirectory, string javaHome)
        {
            var startInfo = CreateProcessStartInfo(batPath, arguments, workingDirectory, javaHome);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("无法启动 Kafka 命令");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(120000))
            {
                try { process.Kill(true); } catch { }
                throw new TimeoutException("Kafka 命令执行超时");
            }

            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"Kafka 命令失败，退出码 {process.ExitCode}" : error.Trim());

            return string.IsNullOrWhiteSpace(output) ? error : output;
        }

        private static ProcessStartInfo CreateProcessStartInfo(
            string executablePath, string arguments, string workingDirectory, string? javaHome = null)
        {
            ProcessStartInfo startInfo;
            var ext = Path.GetExtension(executablePath).ToLowerInvariant();
            if (ext is ".bat" or ".cmd")
            {
                var quotedArgs = string.IsNullOrWhiteSpace(arguments) ? string.Empty : $" {QuoteWindowsPath(arguments)}";
                startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{executablePath}\"{quotedArgs}\"",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            if (!string.IsNullOrWhiteSpace(javaHome))
                startInfo.Environment["JAVA_HOME"] = javaHome;

            return startInfo;
        }

        private static string QuoteWindowsPath(string path) =>
            path.Contains(' ') ? $"\"{path}\"" : path;

        private void EnsurePostgreSQLRuntime(string appsDir)
        {
            var pgDir = Path.Combine(appsDir, "postgresql");
            var binDir = Path.Combine(pgDir, "bin");
            var dataDir = Path.Combine(pgDir, "data");
            var logDir = Path.Combine(pgDir, "log");
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(logDir);

            if (File.Exists(Path.Combine(dataDir, "PG_VERSION")))
                return;

            var initdb = Path.Combine(binDir, "initdb.exe");
            if (!File.Exists(initdb))
                throw new FileNotFoundException("未找到 initdb.exe，请确认 PostgreSQL 已正确解压到应用目录");

            var startInfo = new ProcessStartInfo
            {
                FileName = initdb,
                Arguments = $"-D \"{dataDir}\" -U postgres -A trust -E UTF8 --locale=C",
                WorkingDirectory = binDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("initdb 启动失败");

            if (!process.WaitForExit(120000))
            {
                try { process.Kill(true); } catch { }
                throw new TimeoutException("initdb 初始化超时");
            }

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                    ? $"initdb 初始化失败，退出码 {process.ExitCode}"
                    : error.Trim());
            }
        }

        private static void EnsureRedisConfig(string appsDir)
        {
            var redisDir = Path.Combine(appsDir, "redis");
            Directory.CreateDirectory(redisDir);
            Directory.CreateDirectory(Path.Combine(redisDir, "data"));

            var confPath = Path.Combine(redisDir, "redis.windows.conf");
            if (!File.Exists(confPath))
            {
                File.WriteAllText(confPath,
                    "bind 127.0.0.1\r\n" +
                    "port 6379\r\n" +
                    "dir ./data\r\n" +
                    "appendonly yes\r\n");
            }
        }
    }
}
