using System.Diagnostics;
using System.Text;

namespace DevEnv.Core.Updates;

/// <summary>
/// 自研更新/回滚器：以 <c>DevEnv.exe --updater apply|rollback ...</c> 的第二个进程运行，
/// 不依赖任何外部库。等待主进程退出 → 备份 exe 与 Resources → 覆盖新版载荷 → 启动新版并看门狗校验；
/// 新版 45 秒内没有写 <c>.update_ok</c> 或提前退出时恢复备份并重启旧版。
/// </summary>
public static class Updater
{
    private const int WatchdogSeconds = 45;

    public static int Run(string[] args)
    {
        string exePath;
        try
        {
            exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法定位当前 exe 路径");
            Log($"更新器启动：{Path.GetFileName(exePath)}");
        }
        catch (Exception ex)
        {
            Log("初始化失败：" + ex.Message);
            return 1;
        }

        // 手动回滚会留下上一进程的 .rollback 文件；下次启动时它已不再被占用，顺手清掉。
        TryDeleteFile(exePath + ".rollback");

        string mode = args.Length > 0 ? args[0] : string.Empty;
        try
        {
            return mode switch
            {
                "apply" => Apply(exePath, args),
                "rollback" => ManualRollback(exePath, args),
                _ => Fail($"未知模式：{mode}"),
            };
        }
        catch (Exception ex)
        {
            Log("更新器异常：" + ex);
            RestoreAndRelaunch(exePath, relaunchArgs: null);
            return 1;
        }
    }

    private static int Apply(string exePath, string[] args)
    {
        string fromVersion = GetArg(args, "--from") ?? string.Empty;
        string toVersion = GetArg(args, "--to") ?? string.Empty;
        string payload = GetArg(args, "--payload") ?? string.Empty;
        int pid = ParsePid(args);
        Log($"apply：{fromVersion} -> {toVersion}");

        WaitForExit(pid);

        if (!Directory.Exists(payload))
            throw new InvalidOperationException($"找不到更新载荷：{payload}");

        string installDirectory = Path.GetDirectoryName(exePath)
            ?? throw new InvalidOperationException("无法定位安装目录");
        string resources = Path.Combine(installDirectory, "Resources");
        string resourcesBackup = resources + UpdatePaths.BackupSuffix;
        string exeBackup = exePath + UpdatePaths.BackupSuffix;

        // 运行中的 exe 可改名、不可覆盖：先腾位，才能放入新文件。
        TryDeleteFile(exeBackup);
        File.Move(exePath, exeBackup);
        Log("已备份旧版 exe");

        bool resourcesBackedUp = false;
        try
        {
            if (Directory.Exists(resources))
            {
                TryDeleteDirectory(resourcesBackup);
                Directory.Move(resources, resourcesBackup);
                resourcesBackedUp = true;
                Log("已备份 Resources");
            }
        }
        catch (Exception ex)
        {
            // 资源目录被占用时仍然可以只替换 exe；本次跳过 Resources，避免覆盖一半。
            Log("备份 Resources 失败：" + ex.Message);
        }

        try
        {
            CopyPayload(payload, installDirectory, skipResources: !resourcesBackedUp);
            Log("已覆盖新版载荷");
        }
        catch
        {
            RestoreAndRelaunch(exePath, relaunchArgs: $"--rollback-from {toVersion} --rollback-to {fromVersion}");
            return 1;
        }

        TryDeleteFile(UpdatePaths.MarkerPath);
        using Process? process = StartProcess(exePath, $"--updated-to {toVersion}", out string error);
        if (process is null)
        {
            Log("启动新版本失败：" + error);
            RestoreAndRelaunch(exePath, $"--rollback-from {toVersion} --rollback-to {fromVersion}");
            return 1;
        }

        Log($"已启动新版本 pid={process.Id}，进入看门狗（{WatchdogSeconds}s）");
        DateTime deadline = DateTime.UtcNow.AddSeconds(WatchdogSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (HasExited(process))
            {
                Log("新版本提前退出，回滚");
                RestoreAndRelaunch(exePath, $"--rollback-from {toVersion} --rollback-to {fromVersion}");
                return 1;
            }
            if (File.Exists(UpdatePaths.MarkerPath))
            {
                TryDeleteFile(UpdatePaths.MarkerPath);
                Log("新版本就绪，更新成功");
                return 0;
            }
            Thread.Sleep(500);
        }

        Log("看门狗超时，回滚");
        Kill(process);
        RestoreAndRelaunch(exePath, $"--rollback-from {toVersion} --rollback-to {fromVersion}");
        return 1;
    }

    private static int ManualRollback(string exePath, string[] args)
    {
        Log("手动回滚开始");
        WaitForExit(ParsePid(args));

        string exeBackup = exePath + UpdatePaths.BackupSuffix;
        if (!File.Exists(exeBackup))
        {
            Log("没有可回滚的备份");
            return 1;
        }

        string rolledBack = exePath + ".rollback";
        TryDeleteFile(rolledBack);
        File.Move(exePath, rolledBack);
        File.Move(exeBackup, exePath);
        RestoreResources();
        Log("已恢复备份");
        StartProcess(exePath, "--manual-rollback", out string error);
        if (!string.IsNullOrEmpty(error))
            Log("重启旧版本失败：" + error);
        TryDeleteFile(rolledBack);
        return 0;
    }

    private static void RestoreAndRelaunch(string exePath, string? relaunchArgs)
    {
        string exeBackup = exePath + UpdatePaths.BackupSuffix;
        try
        {
            if (File.Exists(exeBackup))
            {
                TryDeleteFile(exePath);
                File.Move(exeBackup, exePath);
                Log("已恢复备份 exe");
            }
        }
        catch (Exception ex)
        {
            Log("恢复 exe 失败：" + ex.Message);
        }

        RestoreResources();

        StartProcess(exePath, relaunchArgs ?? string.Empty, out string error);
        if (!string.IsNullOrEmpty(error))
            Log("重启失败：" + error);
    }

    private static void RestoreResources()
    {
        string resources = UpdatePaths.ResourcesDirectory;
        string backup = UpdatePaths.ResourcesBackupDirectory;
        try
        {
            if (!Directory.Exists(backup))
                return;
            TryDeleteDirectory(resources);
            Directory.Move(backup, resources);
            Log("已恢复 Resources");
        }
        catch (Exception ex)
        {
            Log("恢复 Resources 失败：" + ex.Message);
        }
    }

    private static void CopyPayload(string payloadDirectory, string installDirectory, bool skipResources)
    {
        string root = Path.GetFullPath(payloadDirectory);
        string rootWithSeparator = root + Path.DirectorySeparatorChar;

        foreach (string source in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string fullSource = Path.GetFullPath(source);
            if (!fullSource.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                continue;

            string relative = Path.GetRelativePath(root, fullSource);
            if (skipResources
                && (relative.StartsWith("Resources" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || relative.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string destination = Path.Combine(installDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            CopyWithRetry(fullSource, destination);
        }
    }

    private static void CopyWithRetry(string source, string destination)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return;
            }
            catch (IOException ex)
            {
                last = ex;
                Thread.Sleep(300 * (attempt + 1));
            }
            catch (UnauthorizedAccessException ex)
            {
                last = ex;
                Thread.Sleep(300 * (attempt + 1));
            }
        }
        throw new IOException($"复制文件失败：{destination}", last);
    }

    private static void WaitForExit(int pid)
    {
        if (pid <= 0)
            return;

        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using Process process = Process.GetProcessById(pid);
                if (process.HasExited)
                    return;
            }
            catch (ArgumentException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log("等待主进程异常：" + ex.Message);
                return;
            }
            Thread.Sleep(300);
        }
        Log("等待主进程退出超时，继续尝试替换");
    }

    private static bool HasExited(Process process)
    {
        try { return process.HasExited; }
        catch { return true; }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static Process? StartProcess(string file, string arguments, out string error)
    {
        error = string.Empty;
        try
        {
            return Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private static string? GetArg(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static int ParsePid(string[] args)
        => int.TryParse(GetArg(args, "--pid"), out int pid) ? pid : 0;

    private static int Fail(string message)
    {
        Log(message);
        return 1;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(UpdatePaths.LocalRoot);
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(UpdatePaths.LogPath, line, Encoding.UTF8);
        }
        catch
        {
            // 日志失败不影响更新流程。
        }
    }
}
