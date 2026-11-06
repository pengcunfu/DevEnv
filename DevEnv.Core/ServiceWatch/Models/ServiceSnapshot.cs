namespace DevEnv.Core.ServiceWatch.Models;

/// <summary>服务列表快照。</summary>
public sealed class ServiceSnapshot
{
    public required IReadOnlyList<ServiceInfo> Services { get; init; }
    public DateTime Timestamp { get; init; }
    public int TotalCount { get; init; }
    public int RunningCount { get; init; }
    public int StoppedCount { get; init; }
    public bool IsElevated { get; init; }
    public string? StatusHint { get; init; }
}
