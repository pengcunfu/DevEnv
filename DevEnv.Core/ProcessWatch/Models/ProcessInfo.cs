namespace DevEnv.Core.ProcessWatch.Models;

/// <summary>单条进程快照，供列表与详情绑定。</summary>
public sealed class ProcessInfo
{
    public required string Name { get; init; }
    public required int ProcessId { get; init; }
    public double CpuPercent { get; init; }
    public long WorkingSetBytes { get; init; }
    public long PrivateMemoryBytes { get; init; }
    public long VirtualMemoryBytes { get; init; }
    public int ThreadCount { get; init; }
    public int HandleCount { get; init; }
    public string? ExecutablePath { get; init; }
    public DateTime? StartTime { get; init; }
    public bool Responding { get; init; } = true;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"PID {ProcessId}" : Name;
    public string CpuText => $"{CpuPercent:0.0} %";
    public string MemoryText => ByteFormatter.FormatBytes(WorkingSetBytes);
    public string PrivateMemoryText => ByteFormatter.FormatBytes(PrivateMemoryBytes);
    public string VirtualMemoryText => ByteFormatter.FormatBytes(VirtualMemoryBytes);
    public string PathText => ExecutablePath ?? "";
    public string StartTimeText => StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
    public string RespondingText => Responding ? "响应" : "无响应";
}
