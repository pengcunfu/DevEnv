namespace DevEnv.Host.ViewModels;

/// <summary>左侧导航一项。</summary>
public sealed class NavItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }

    public override string ToString() => Title;
}