namespace ProcessWatch.Models;

/// <summary>一次全量进程采样。</summary>
public sealed class ProcessSnapshot
{
    public required IReadOnlyList<ProcessInfo> Processes { get; init; }
    public required DateTime Timestamp { get; init; }
    public required int ProcessCount { get; init; }
    public required long TotalWorkingSetBytes { get; init; }
    public string? StatusHint { get; init; }

    public string TotalMemoryText => ByteFormatter.FormatBytes(TotalWorkingSetBytes);
}
