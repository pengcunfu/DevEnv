namespace DevBox.Host.ViewModels;

/// <summary>页内页签（网络监控的流量/端口/测速）。</summary>
public sealed class NavTab
{
    public required string Id { get; init; }
    public required string Title { get; init; }
}