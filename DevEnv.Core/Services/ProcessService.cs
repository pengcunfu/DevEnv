using System.Diagnostics;
using DevEnv.Core.Models;
using DevEnv.Core.ProcessWatch.Models;
using DevEnv.Core.ProcessWatch.Services;

namespace DevEnv.Core.Services;

/// <summary>进程监控：快照、结束进程、定位目录。逻辑来自 <c>ProcessEndpoints</c>。</summary>
public sealed class ProcessService
{
    private readonly object _gate = new();
    private readonly ProcessMonitorService _monitor = new() { SortMode = ProcessSortMode.Cpu };

    public ProcessService()
    {
        // 先预热一次采样，第二次起才有 CPU 增量
        _monitor.CollectSnapshot();
    }

    /// <summary>采集全量进程快照。会在后台线程调用，用锁串行化避免采样状态互相踩踏。</summary>
    public ProcessSnapshotView GetSnapshot(string? sort = null)
    {
        lock (_gate)
        {
            _monitor.SortMode = sort?.ToLowerInvariant() switch
            {
                "memory" => ProcessSortMode.Memory,
                "name" => ProcessSortMode.Name,
                _ => ProcessSortMode.Cpu,
            };

            return ToView(_monitor.CollectSnapshot());
        }
    }

    private static ProcessSnapshotView ToView(ProcessSnapshot snapshot) => new()
    {
            ProcessCount = snapshot.ProcessCount,
            TotalMemoryText = snapshot.TotalMemoryText,
            Timestamp = snapshot.Timestamp,
            StatusHint = snapshot.StatusHint,
            Processes = snapshot.Processes.Select(p => new ProcessRowView
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                ProcessId = p.ProcessId,
                CpuPercent = p.CpuPercent,
                CpuText = p.CpuText,
                WorkingSetBytes = p.WorkingSetBytes,
                MemoryText = p.MemoryText,
                PrivateMemoryText = p.PrivateMemoryText,
                VirtualMemoryText = p.VirtualMemoryText,
                ThreadCount = p.ThreadCount,
                HandleCount = p.HandleCount,
                Path = p.ExecutablePath,
                PathText = p.PathText,
                StartTimeText = p.StartTimeText,
                RespondingText = p.RespondingText,
            }).ToList(),
    };

    public OperationResult Kill(int pid, bool entireProcessTree) =>
        ProcessActions.TryKill(pid, entireProcessTree, out var error)
            ? OperationResult.Success()
            : OperationResult.Fail(error ?? "结束进程失败");

    public OperationResult OpenFolder(int pid)
    {
        string? path = null;
        try
        {
            using var proc = Process.GetProcessById(pid);
            path = proc.MainModule?.FileName;
        }
        catch
        {
            // 拿不到路径（权限不足 / 已退出）
        }

        return ProcessActions.TryOpenFolder(path, out var error)
            ? OperationResult.Success()
            : OperationResult.Fail(error ?? "无法打开目录");
    }
}