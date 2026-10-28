using System.Collections.Concurrent;
using System.Diagnostics;
using DevEnv.Core.Models;

namespace DevEnv.Core.Services;

/// <summary>
/// 开发环境服务：用户自定义后台进程的配置与启停。
/// 启动命令交给 <c>cmd /c</c> 执行，因此 npm / 批处理 / 带参数的 exe 都能直接用；
/// 标准输出与错误输出实时写入日志，内存与磁盘各留一份。
/// </summary>
public sealed class DevEnvService
{
    /// <summary>本程序拉起的进程（按配置 Id 索引），用于停止时结束整棵进程树。</summary>
    private readonly ConcurrentDictionary<string, Process> _started = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>各服务的运行日志（按配置 Id 索引），停止后仍保留供查看。</summary>
    private readonly ConcurrentDictionary<string, DevServiceLog> _logs = new(StringComparer.OrdinalIgnoreCase);

    private readonly DevServiceStore _store;

    public DevEnvService(DevServiceStore store) => _store = store;

    public IReadOnlyList<DevServiceItem> GetServices() => _store.GetAll().Select(ToItem).ToList();

    public DevServiceConfig? GetConfig(string id)
    {
        var config = Find(id);
        return config is null ? null : Clone(config);
    }

    public OperationResult Save(DevServiceConfig config)
    {
        var normalized = Normalize(config);
        if (normalized.Name.Length == 0)
            return OperationResult.Fail("请填写服务名称");
        if (normalized.StartCommand.Length == 0)
            return OperationResult.Fail("请填写启动命令");
        if (normalized.WorkingDirectory.Length > 0 && !Directory.Exists(normalized.WorkingDirectory))
            return OperationResult.Fail($"工作目录不存在：{normalized.WorkingDirectory}");

        var list = _store.GetAll().ToList();
        var index = list.FindIndex(c => SameId(c.Id, normalized.Id));
        if (index >= 0)
            list[index] = normalized;
        else
            list.Add(normalized);
        _store.SaveAll(list);

        return OperationResult.Success(index >= 0 ? "已保存" : $"{DisplayName(normalized)} 已添加");
    }

    public OperationResult Remove(string id)
    {
        var config = Find(id);
        if (config is null)
            return OperationResult.Fail("配置不存在");

        var list = _store.GetAll().ToList();
        list.RemoveAll(c => SameId(c.Id, id));
        _store.SaveAll(list);

        RemoveTracked(id);
        if (_logs.TryRemove(id, out var log))
            log.Dispose();
        DevServiceLog.Delete(GetLogPath(config));
        return OperationResult.Success("已移除");
    }

    public OperationResult Start(string id)
    {
        var config = Find(id);
        if (config is null)
            return OperationResult.Fail("配置不存在");

        var name = DisplayName(config);
        if (IsRunning(config))
            return OperationResult.Success($"{name} 已在运行");
        if (config.StartCommand.Length == 0)
            return OperationResult.Fail("未配置启动命令");

        var workingDirectory = ResolveWorkingDirectory(config);
        if (!Directory.Exists(workingDirectory))
            return OperationResult.Fail($"工作目录不存在：{workingDirectory}");

        try
        {
            var log = BeginLog(config);
            log.AppendSystem($"启动：{config.StartCommand}");
            log.AppendSystem($"工作目录：{workingDirectory}");

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // chcp 65001 让 cmd 与多数开发工具按 UTF-8 输出，中文日志才不会乱码。
                Arguments = "/c chcp 65001>nul & " + config.StartCommand,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null)
            {
                log.AppendSystem("启动失败：无法创建进程");
                return OperationResult.Fail("启动失败：无法创建进程");
            }

            var pumps = AttachLog(process, log);

            // 命令秒退且非 0 多半是路径或参数写错，直接反馈退出码。
            if (process.WaitForExit(800))
            {
                // 等管道读空，日志里才能看到具体报错原因。
                try
                {
                    Task.WaitAll([pumps.Stdout, pumps.Stderr], 2000);
                }
                catch
                {
                    // ignore
                }

                var exitCode = process.ExitCode;
                process.Dispose();
                return exitCode != 0
                    ? OperationResult.Fail($"{name} 启动失败（退出代码 {exitCode}），详情见日志")
                    : OperationResult.Success($"{name} 的启动命令已执行并退出，未保持后台运行");
            }

            ReplaceTracked(config.Id, process);
            WaitForProcessName(config, 2000);
            return OperationResult.Success($"{name} 已启动");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult Stop(string id)
    {
        var config = Find(id);
        if (config is null)
            return OperationResult.Fail("配置不存在");

        var name = DisplayName(config);
        var log = _logs.TryGetValue(id, out var existingLog) ? existingLog : null;
        if (!IsRunning(config))
        {
            RemoveTracked(id);
            log?.AppendSystem($"{name} 当前未运行");
            return OperationResult.Success($"{name} 当前未运行");
        }

        // 1. 优先走配置的停止命令，让数据库之类能优雅退出。
        if (config.StopCommand.Length > 0)
        {
            log?.AppendSystem($"执行停止命令：{config.StopCommand}");
            try
            {
                RunAndWait(config.StopCommand, ResolveWorkingDirectory(config), 20000);
            }
            catch
            {
                // 停止命令失败时继续走结束进程的兜底逻辑。
            }

            WaitUntilStopped(config, 5000);
        }

        // 2. 结束本程序拉起的进程树。
        if (IsRunning(config) && _started.TryGetValue(config.Id, out var tracked))
            KillProcess(tracked);

        // 3. 按进程名兜底（也能停掉本程序重启前启动的进程）。
        if (IsRunning(config) && NormalizeProcessName(config.ProcessName).Length > 0)
            KillProcessesByName(NormalizeProcessName(config.ProcessName));

        WaitUntilStopped(config, 5000);
        if (IsRunning(config))
        {
            log?.AppendSystem($"无法停止 {name}，请手动结束进程");
            return OperationResult.Fail($"无法停止 {name}，请手动结束进程");
        }

        log?.AppendSystem($"{name} 已停止");
        return OperationResult.Success($"{name} 已停止");
    }

    /// <summary>读取服务日志：优先内存（本次运行），否则回读磁盘上的上一次运行记录。</summary>
    public string GetLogText(string id)
    {
        if (_logs.TryGetValue(id, out var log))
            return log.GetText();

        var config = Find(id);
        return config is null ? string.Empty : DevServiceLog.ReadTail(GetLogPath(config));
    }

    public void ClearLog(string id)
    {
        if (_logs.TryGetValue(id, out var log))
        {
            log.Clear();
            return;
        }

        var config = Find(id);
        if (config is not null)
            DevServiceLog.Delete(GetLogPath(config));
    }

    public OperationResult OpenLogsDirectory()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{AppPaths.LogsDirectory}\"",
                UseShellExecute = true,
            });
            return OperationResult.Success(AppPaths.LogsDirectory);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult OpenWorkingDirectory(string id)
    {
        var config = Find(id);
        if (config is null)
            return OperationResult.Fail("配置不存在");

        var dir = ResolveWorkingDirectory(config);
        if (!Directory.Exists(dir))
            return OperationResult.Fail("工作目录不存在");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{dir}\"",
                UseShellExecute = true,
            });
            return OperationResult.Success(dir);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private DevServiceItem ToItem(DevServiceConfig config)
    {
        var running = IsRunning(config);
        var hasCommand = config.StartCommand.Length > 0;
        return new DevServiceItem
        {
            Id = config.Id,
            Name = DisplayName(config),
            WorkingDirectory = ResolveWorkingDirectory(config),
            StartCommand = config.StartCommand,
            StopCommand = config.StopCommand,
            ProcessName = config.ProcessName,
            Running = running,
            StatusText = !hasCommand ? "未配置命令" : running ? "运行中" : "已停止",
            CanStart = hasCommand && !running,
            CanStop = running,
        };
    }

    private DevServiceConfig? Find(string id) =>
        _store.GetAll().FirstOrDefault(c => SameId(c.Id, id));

    private bool IsRunning(DevServiceConfig config)
    {
        if (IsTrackedRunning(config.Id))
            return true;

        var processName = NormalizeProcessName(config.ProcessName);
        if (processName.Length == 0)
            return false;

        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private bool IsTrackedRunning(string id)
    {
        if (!_started.TryGetValue(id, out var process))
            return false;

        try
        {
            if (!process.HasExited)
                return true;
        }
        catch
        {
            // 进程对象已失效，按已退出处理。
        }

        _started.TryRemove(id, out _);
        process.Dispose();
        return false;
    }

    /// <summary>启动命令可能是 npm 之类的壳，进程名要稍等才会出现；这里只做有限轮询。</summary>
    private static void WaitForProcessName(DevServiceConfig config, int timeoutMs)
    {
        var processName = NormalizeProcessName(config.ProcessName);
        if (processName.Length == 0)
            return;

        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            try
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                    return;
            }
            catch
            {
                return;
            }

            Thread.Sleep(100);
        }
    }

    private void WaitUntilStopped(DevServiceConfig config, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (!IsRunning(config))
                return;
            Thread.Sleep(200);
        }
    }

    /// <summary>把标准输出 / 错误输出接进日志，并在进程结束时补一条退出记录。</summary>
    private static (Task Stdout, Task Stderr) AttachLog(Process process, DevServiceLog log)
    {
        // 自己读原始字节再按行猜编码：PostgreSQL 等原生程序输出 GBK，npm / node 输出 UTF-8，
        // 固定用某一种编码必然有一边乱码，BeginOutputReadLine 又只能按固定编码解码。
        var stdout = Task.Run(() => PumpOutput(process.StandardOutput.BaseStream, log, isError: false));
        var stderr = Task.Run(() => PumpOutput(process.StandardError.BaseStream, log, isError: true));

        _ = Task.Run(() =>
        {
            try
            {
                process.WaitForExit();
                Task.WaitAll(stdout, stderr);
                log.AppendSystem($"进程已退出（退出代码 {process.ExitCode}）");
            }
            catch
            {
                // 进程对象可能已被释放，忽略。
            }
        });

        return (stdout, stderr);
    }

    /// <summary>按行读取管道原始字节，交给 <see cref="DevServiceLog.DecodeLine"/> 解码。</summary>
    private static void PumpOutput(Stream stream, DevServiceLog log, bool isError)
    {
        var buffer = new byte[4096];
        var line = new List<byte>(256);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    var value = buffer[i];
                    if (value == (byte)'\n')
                    {
                        log.Append(DevServiceLog.DecodeLine(line.ToArray()), isError);
                        line.Clear();
                    }
                    else if (value != (byte)'\r')
                    {
                        line.Add(value);
                    }
                }
            }

            // 最后一行可能没有换行符。
            if (line.Count > 0)
                log.Append(DevServiceLog.DecodeLine(line.ToArray()), isError);
        }
        catch
        {
            // 进程退出时管道关闭，忽略。
        }
    }

    /// <summary>每次启动都新开一份日志（同名文件会被截断），旧的内存缓冲先释放。</summary>
    private DevServiceLog BeginLog(DevServiceConfig config)
    {
        if (_logs.TryRemove(config.Id, out var previous))
            previous.Dispose();

        var log = new DevServiceLog(GetLogPath(config));
        _logs[config.Id] = log;
        return log;
    }

    private static string GetLogPath(DevServiceConfig config) =>
        DevServiceLog.GetLogPath(DisplayName(config), config.Id);

    private void ReplaceTracked(string id, Process process)
    {
        if (_started.TryGetValue(id, out var previous))
            previous.Dispose();
        _started[id] = process;
    }

    private void RemoveTracked(string id)
    {
        if (_started.TryRemove(id, out var process))
            process.Dispose();
    }

    private static void RunAndWait(string command, string workingDirectory, int timeoutMs)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + command,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        process?.WaitForExit(timeoutMs);
    }

    private static void KillProcess(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        {
            // 进程可能已经退出，忽略。
        }
    }

    private static void KillProcessesByName(string processName)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch
        {
            return;
        }

        foreach (var process in processes)
        {
            using (process)
                KillProcess(process);
        }
    }

    private static string ResolveWorkingDirectory(DevServiceConfig config) =>
        config.WorkingDirectory.Length > 0 ? config.WorkingDirectory : AppPaths.UserDataDirectory;

    private static string DisplayName(DevServiceConfig config) =>
        config.Name.Length > 0 ? config.Name : config.StartCommand.Length > 0 ? config.StartCommand : "未命名服务";

    private static bool SameId(string left, string right) =>
        left.Length > 0 && right.Length > 0 && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static DevServiceConfig Normalize(DevServiceConfig config) => new()
    {
        Id = string.IsNullOrWhiteSpace(config.Id) ? Guid.NewGuid().ToString("N") : config.Id.Trim(),
        Name = config.Name?.Trim() ?? string.Empty,
        WorkingDirectory = config.WorkingDirectory?.Trim().Trim('"') ?? string.Empty,
        StartCommand = config.StartCommand?.Trim() ?? string.Empty,
        StopCommand = config.StopCommand?.Trim() ?? string.Empty,
        ProcessName = NormalizeProcessName(config.ProcessName),
    };

    private static DevServiceConfig Clone(DevServiceConfig config) => new()
    {
        Id = config.Id,
        Name = config.Name,
        WorkingDirectory = config.WorkingDirectory,
        StartCommand = config.StartCommand,
        StopCommand = config.StopCommand,
        ProcessName = config.ProcessName,
    };

    private static string NormalizeProcessName(string? value)
    {
        var name = (value ?? string.Empty).Trim().Trim('"');
        if (name.Length == 0)
            return string.Empty;

        name = Path.GetFileName(name);
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }
}
