namespace DevEnv.Core.Services;

/// <summary>开始菜单快捷方式的类型。</summary>
public enum StartMenuLinkKind
{
    /// <summary>普通 .lnk 快捷方式。</summary>
    Shortcut,

    /// <summary>.url 网址快捷方式。</summary>
    InternetShortcut,
}

/// <summary>
/// 开始菜单里的一条快捷方式。索引只负责文件系统结构，
/// 目标路径与图标由 <see cref="StartMenuService"/> 在读取时补齐。
/// </summary>
public sealed class StartMenuItem
{
    /// <summary>快捷方式显示名（文件名去掉扩展名）。</summary>
    public required string Name { get; init; }

    /// <summary>.lnk / .url 的完整路径。</summary>
    public required string LinkPath { get; init; }

    /// <summary>相对 Programs 的路径，用作跨用户目录合并与页面内稳定标识。</summary>
    public required string RelativePath { get; init; }

    /// <summary>Programs 下的一级文件夹名；根目录零散条目为空。</summary>
    public string FirstLevelFolder { get; init; } = string.Empty;

    /// <summary>一级文件夹以下的子目录；没有更深层级时为空。</summary>
    public string SubFolder { get; init; } = string.Empty;

    /// <summary>.lnk 解析出的目标路径；.url 为网址。解析失败时为空。</summary>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>图标缓存文件路径；提取失败时为空。</summary>
    public string IconPath { get; set; } = string.Empty;

    public StartMenuLinkKind Kind { get; init; } = StartMenuLinkKind.Shortcut;

    /// <summary>.lnk 目标是否解析成功。</summary>
    public bool IsResolved { get; set; }

    /// <summary>.url 条目对应的网址；普通快捷方式为空。</summary>
    public string Url => Kind == StartMenuLinkKind.InternetShortcut ? TargetPath : string.Empty;

    /// <summary>页面内的稳定标识；只用于这次加载的内存列表，不写入布局。</summary>
    public string Key => $"startmenu|{RelativePath}";
}
