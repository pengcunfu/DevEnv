using System.Diagnostics;
using System.Text;
using DevEnv.Core.FileTools.Models;
using DevEnv.Core.FileTools.Native;
using DevEnv.Core.Models;
using DevEnv.Core.ProcessWatch.Services;

namespace DevEnv.Core.FileTools.Services;

/// <summary>
/// 文件占用检测：通过 Restart Manager 找出正在使用某文件 / 文件夹内文件的进程，
/// 并按用户选择结束这些进程。检测本身只读，不会改动文件。
/// </summary>
public sealed class FileUnlockService
{
    /// <summary>扫描文件夹时最多提交给 Restart Manager 的文件数，避免超大目录卡死界面。</summary>
    public const int MaxFilesPerScan = 2000;

    /// <summary>检测路径（文件或文件夹）被哪些进程占用。</summary>
    public (OperationResult Result, FileLockScanResult? Scan) Scan(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (OperationResult.Fail("请选择要检测的文件或文件夹"), null);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"路径无效：{ex.Message}"), null);
        }

        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            return (OperationResult.Fail("路径不存在"), null);

        var (files, warnings, truncated) = CollectFiles(fullPath);
        if (files.Count == 0)
            return (OperationResult.Fail("没有可检测的文件"), null);

        var key = new StringBuilder(RestartManagerNative.CchRmSessionKey + 1);
        var startResult = RestartManagerNative.RmStartSession(out var session, 0, key);
        if (startResult != RestartManagerNative.ErrorSuccess)
            return (OperationResult.Fail($"无法启动占用检测（错误码 {startResult}）"), null);

        try
        {
            var fileArray = files.ToArray();
            var registerResult = RestartManagerNative.RmRegisterResources(
                session,
                (uint)fileArray.Length,
                fileArray,
                0,
                null,
                0,
                null);
            if (registerResult != RestartManagerNative.ErrorSuccess)
                return (OperationResult.Fail($"无法注册检测文件（错误码 {registerResult}）"), null);

            var (listResult, processes) = QueryProcesses(session);
            if (!listResult.Ok)
                return (listResult, null);

            return (OperationResult.Success(), new FileLockScanResult
            {
                Processes = processes,
                Warnings = warnings,
                RegisteredFileCount = files.Count,
                Truncated = truncated,
            });
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail($"占用检测失败：{ex.Message}"), null);
        }
        finally
        {
            RestartManagerNative.RmEndSession(session);
        }
    }

    /// <summary>结束占用进程；关键进程与当前程序自身由调用方先行拦截。</summary>
    public OperationResult EndProcess(LockedProcessInfo process, bool entireProcessTree = false)
    {
        if (process.IsCurrentProcess)
            return OperationResult.Fail("不能结束当前程序自身");
        if (process.IsCritical)
            return OperationResult.Fail("关键系统进程不允许在这里结束");
        return EndProcess(process.ProcessId, entireProcessTree);
    }

    public OperationResult EndProcess(int processId, bool entireProcessTree = false)
    {
        if (processId == Environment.ProcessId)
            return OperationResult.Fail("不能结束当前程序自身");
        if (processId <= 4)
            return OperationResult.Fail("不能结束系统关键进程");

        return ProcessActions.TryKill(processId, entireProcessTree, out var error)
            ? OperationResult.Success("已结束进程")
            : OperationResult.Fail(error ?? "结束进程失败");
    }

    private static (OperationResult Result, IReadOnlyList<LockedProcessInfo> Processes) QueryProcesses(uint session)
    {
        // RmGetList 的调用约定是「先问数量、再取列表」。若期间又有进程打开文件，
        // 第二次调用会再次返回 ERROR_MORE_DATA，此时最多重试几次。
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint needed = 0;
            uint count = 0;
            var result = RestartManagerNative.RmGetList(
                session,
                out needed,
                ref count,
                null,
                out _);

            if (result == RestartManagerNative.ErrorSuccess)
                return (OperationResult.Success(), []);
            if (result != RestartManagerNative.ErrorMoreData)
                return (OperationResult.Fail($"读取占用进程失败（错误码 {result}）"), []);
            if (needed == 0)
                return (OperationResult.Success(), []);

            var buffer = new RestartManagerNative.RmProcessInfo[needed];
            count = needed;
            result = RestartManagerNative.RmGetList(
                session,
                out needed,
                ref count,
                buffer,
                out _);

            if (result == RestartManagerNative.ErrorSuccess)
                return (OperationResult.Success(), MapProcesses(buffer, count));
            if (result != RestartManagerNative.ErrorMoreData)
                return (OperationResult.Fail($"读取占用进程失败（错误码 {result}）"), []);
        }

        return (OperationResult.Fail("占用进程列表持续变化，请重新检测"), []);
    }

    private static IReadOnlyList<LockedProcessInfo> MapProcesses(
        RestartManagerNative.RmProcessInfo[] buffer,
        uint count)
    {
        var seen = new HashSet<int>();
        var processes = new List<LockedProcessInfo>();
        var limit = (int)Math.Min(count, (uint)buffer.Length);
        for (var i = 0; i < limit; i++)
        {
            var raw = buffer[i];
            if (raw.Process.ProcessId <= 0 || !seen.Add(raw.Process.ProcessId))
                continue;
            processes.Add(CreateView(raw));
        }

        return processes;
    }

    private static LockedProcessInfo CreateView(RestartManagerNative.RmProcessInfo raw)
    {
        var pid = raw.Process.ProcessId;
        var name = raw.AppName;
        var path = string.Empty;
        var startTime = string.Empty;

        try
        {
            using var process = Process.GetProcessById(pid);
            if (string.IsNullOrWhiteSpace(name))
                name = process.ProcessName;
            try
            {
                path = process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                // 权限不足或进程已退出：路径留空
            }

            try
            {
                startTime = process.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                // 部分受保护进程读不到启动时间
            }
        }
        catch
        {
            // 进程可能刚好退出
        }

        if (string.IsNullOrWhiteSpace(name))
            name = $"PID {pid}";

        return new LockedProcessInfo
        {
            ProcessId = pid,
            Name = name,
            ExecutablePath = path,
            AppTypeText = DescribeAppType(raw.ApplicationType),
            IsService = raw.ApplicationType == RestartManagerNative.RmAppType.RmService,
            IsCritical = raw.ApplicationType == RestartManagerNative.RmAppType.RmCritical,
            IsCurrentProcess = pid == Environment.ProcessId,
            StartTimeText = startTime,
        };
    }

    private static string DescribeAppType(RestartManagerNative.RmAppType type) => type switch
    {
        RestartManagerNative.RmAppType.RmMainWindow => "窗口程序",
        RestartManagerNative.RmAppType.RmOtherWindow => "后台窗口",
        RestartManagerNative.RmAppType.RmService => "系统服务",
        RestartManagerNative.RmAppType.RmExplorer => "资源管理器",
        RestartManagerNative.RmAppType.RmConsole => "控制台程序",
        RestartManagerNative.RmAppType.RmCritical => "关键系统进程",
        _ => "未知类型",
    };

    private static (List<string> Files, List<string> Warnings, bool Truncated) CollectFiles(string fullPath)
    {
        var files = new List<string>();
        var warnings = new List<string>();
        var truncated = false;

        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(fullPath);
        }
        catch (Exception ex)
        {
            warnings.Add($"无法读取路径属性：{ex.Message}");
            return (files, warnings, false);
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            files.Add(fullPath);
            return (files, warnings, false);
        }

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                ReturnSpecialDirectories = false,
            };

            foreach (var file in Directory.EnumerateFiles(fullPath, "*", options))
            {
                if (files.Count >= MaxFilesPerScan)
                {
                    truncated = true;
                    break;
                }
                files.Add(file);
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"文件夹枚举不完整：{ex.Message}");
        }

        return (files, warnings, truncated);
    }
}
