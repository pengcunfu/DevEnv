namespace DevBox.Core.Models;

/// <summary>进程监控快照（ProcessWatch 原始模型的视图投影）。</summary>
public sealed class ProcessSnapshotView
{
    public int ProcessCount { get; init; }
    public string TotalMemoryText { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string? StatusHint { get; init; }
    public IReadOnlyList<ProcessRowView> Processes { get; init; } = [];
}

public sealed class ProcessRowView
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public double CpuPercent { get; init; }
    public string CpuText { get; init; } = string.Empty;
    public long WorkingSetBytes { get; init; }
    public string MemoryText { get; init; } = string.Empty;
    public string PrivateMemoryText { get; init; } = string.Empty;
    public string VirtualMemoryText { get; init; } = string.Empty;
    public int ThreadCount { get; init; }
    public int HandleCount { get; init; }
    public string? Path { get; init; }
    public string PathText { get; init; } = string.Empty;
    public string StartTimeText { get; init; } = string.Empty;
    public string RespondingText { get; init; } = string.Empty;
}

public sealed class NetworkConnectionView
{
    public string Protocol { get; init; } = string.Empty;
    public string LocalEndpoint { get; init; } = string.Empty;
    public string RemoteEndpoint { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string DownloadRateText { get; init; } = string.Empty;
    public string UploadRateText { get; init; } = string.Empty;
}

public sealed class NetworkProcessView
{
    public int ProcessId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public string? ExecutablePath { get; init; }
    public int ConnectionCount { get; init; }
    public string DownloadRateText { get; init; } = string.Empty;
    public string UploadRateText { get; init; } = string.Empty;
    public IReadOnlyList<NetworkConnectionView> Connections { get; init; } = [];
}

public sealed class NetworkSnapshotView
{
    public DateTime Timestamp { get; init; }
    public int TotalConnections { get; init; }
    public string TotalDownloadText { get; init; } = string.Empty;
    public string TotalUploadText { get; init; } = string.Empty;
    public bool IsElevated { get; init; }
    public string? StatusHint { get; init; }
    public IReadOnlyList<NetworkProcessView> Processes { get; init; } = [];
}

public sealed class PortRowView
{
    public string Protocol { get; init; } = string.Empty;
    public string LocalAddress { get; init; } = string.Empty;
    public int LocalPort { get; init; }
    public string RemoteAddress { get; init; } = string.Empty;
    public int RemotePort { get; init; }
    public string State { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public string LocalEndpoint { get; init; } = string.Empty;
    public string RemoteEndpoint { get; init; } = string.Empty;
}

public sealed class SpeedTestProgressView
{
    public string Phase { get; init; } = string.Empty;
    public double Percent { get; init; }
    public double OverallPercent { get; init; }
    public string CurrentSpeedText { get; init; } = string.Empty;
    public string? Message { get; init; }
}

public sealed class SpeedTestResultView
{
    public string DownloadText { get; init; } = string.Empty;
    public string UploadText { get; init; } = string.Empty;
    public string PingText { get; init; } = string.Empty;
    public string JitterText { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;
    public string DurationText { get; init; } = string.Empty;
    public string? UploadMessage { get; init; }
}

public sealed class SpeedTestStatusView
{
    public bool Running { get; init; }
    public SpeedTestProgressView? Progress { get; init; }
    public SpeedTestResultView? Result { get; init; }
    public string? Error { get; init; }
}