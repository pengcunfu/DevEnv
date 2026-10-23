using System.Diagnostics;
using DevEnv.Core.ProcessWatch.Models;

namespace DevEnv.Core.ProcessWatch.Services;

/// <summary>
/// 后台定时采集进程列表；用跨采样的 TotalProcessorTime 差值估算 CPU%。
/// </summary>
public sealed class ProcessMonitorService : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<int, TimeSpan> _previousCpu = new();
    private DateTime _previousSampleUtc = DateTime.MinValue;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _disposed;

    public ProcessSortMode SortMode { get; set; } = ProcessSortMode.Cpu;
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    public event Action<ProcessSnapshot>? SnapshotUpdated;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cts is not null)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loop = Task.Run(() => RunLoopAsync(token), token);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _cts?.Dispose(); } catch { /* ignore */ }
        _cts = null;
        _loop = null;
        SnapshotUpdated = null;
    }

    public ProcessSnapshot CollectSnapshot()
    {
        lock (_gate)
            return CollectSnapshotCore();
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var snapshot = CollectSnapshot();
                SnapshotUpdated?.Invoke(snapshot);
            }
            catch
            {
                // 单次采样失败不中断循环
            }

            try
            {
                await Task.Delay(Interval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private ProcessSnapshot CollectSnapshotCore()
    {
        var now = DateTime.UtcNow;
        var wallDelta = _previousSampleUtc == DateTime.MinValue
            ? TimeSpan.Zero
            : now - _previousSampleUtc;
        var wallSeconds = wallDelta.TotalSeconds;
        var cores = Math.Max(1, Environment.ProcessorCount);

        var currentCpu = new Dictionary<int, TimeSpan>();
        var list = new List<ProcessInfo>();
        long totalWs = 0;
        var skipped = 0;

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return new ProcessSnapshot
            {
                Processes = Array.Empty<ProcessInfo>(),
                Timestamp = DateTime.Now,
                ProcessCount = 0,
                TotalWorkingSetBytes = 0,
                StatusHint = "无法枚举进程",
            };
        }

        foreach (var proc in processes)
        {
            try
            {
                var pid = proc.Id;
                var name = Safe(() => proc.ProcessName) ?? $"PID {pid}";

                TimeSpan cpuTime = default;
                var hasCpu = false;
                try
                {
                    cpuTime = proc.TotalProcessorTime;
                    hasCpu = true;
                    currentCpu[pid] = cpuTime;
                }
                catch
                {
                    skipped++;
                }

                double cpuPercent = 0;
                if (hasCpu && wallSeconds > 0 && _previousCpu.TryGetValue(pid, out var prev))
                {
                    var delta = (cpuTime - prev).TotalSeconds;
                    if (delta < 0)
                        delta = 0;
                    cpuPercent = Math.Min((delta / (wallSeconds * cores)) * 100.0, 100.0 * cores);
                }

                long workingSet = 0;
                long privateMem = 0;
                long virtualMem = 0;
                var threadCount = 0;
                var handleCount = 0;
                string? path = null;
                DateTime? startTime = null;
                var responding = true;

                try { workingSet = proc.WorkingSet64; } catch { /* ignore */ }
                try { privateMem = proc.PrivateMemorySize64; } catch { /* ignore */ }
                try { virtualMem = proc.VirtualMemorySize64; } catch { /* ignore */ }
                try { threadCount = proc.Threads.Count; } catch { /* ignore */ }
                try { handleCount = proc.HandleCount; } catch { /* ignore */ }
                try { path = proc.MainModule?.FileName; } catch { /* ignore */ }
                try { startTime = proc.StartTime; } catch { /* ignore */ }
                try { responding = proc.Responding; } catch { /* ignore */ }

                totalWs += workingSet;

                list.Add(new ProcessInfo
                {
                    Name = name,
                    ProcessId = pid,
                    CpuPercent = cpuPercent,
                    WorkingSetBytes = workingSet,
                    PrivateMemoryBytes = privateMem,
                    VirtualMemoryBytes = virtualMem,
                    ThreadCount = threadCount,
                    HandleCount = handleCount,
                    ExecutablePath = path,
                    StartTime = startTime,
                    Responding = responding,
                });
            }
            finally
            {
                try { proc.Dispose(); } catch { /* ignore */ }
            }
        }

        _previousCpu.Clear();
        foreach (var (pid, cpu) in currentCpu)
            _previousCpu[pid] = cpu;
        _previousSampleUtc = now;

        IEnumerable<ProcessInfo> ordered = SortMode switch
        {
            ProcessSortMode.Memory => list.OrderByDescending(p => p.WorkingSetBytes),
            ProcessSortMode.Name => list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
            _ => list.OrderByDescending(p => p.CpuPercent).ThenByDescending(p => p.WorkingSetBytes),
        };

        var sorted = ordered.ToList();
        string? hint = null;
        if (wallSeconds <= 0)
            hint = "首次采样中，下一秒显示 CPU%";
        else if (skipped > 0)
            hint = $"{skipped} 个进程无法读取 CPU";

        return new ProcessSnapshot
        {
            Processes = sorted,
            Timestamp = DateTime.Now,
            ProcessCount = sorted.Count,
            TotalWorkingSetBytes = totalWs,
            StatusHint = hint,
        };
    }

    private static T? Safe<T>(Func<T> getter)
    {
        try { return getter(); }
        catch { return default; }
    }
}
