namespace DevEnv.Host.ViewModels;

/// <summary>左侧导航一项。</summary>
public sealed class NavItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    /// <summary>Segoe MDL2 Assets 字形码位，显示在标题左侧。</summary>
    public required string Glyph { get; init; }

    public override string ToString() => Title;
}