namespace DevEnv.Core.Models;

/// <summary>本地应用快捷方式（data.json 的 NativeAtomList）。</summary>
public sealed class NativeAppItem
{
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExecPath { get; set; } = string.Empty;

    /// <summary>我们创建的开始菜单快捷方式文件名（位于「开始菜单\程序\燧岚开发盒子」）；空表示未托管。</summary>
    public string StartMenuLink { get; set; } = string.Empty;

    /// <summary>是否已完成开始菜单同步检查；旧数据默认 false，启动时补建一次。</summary>
    public bool StartMenuChecked { get; set; }
}

/// <summary>文件夹快捷方式（FolderAtomList）。</summary>
public sealed class FolderItem
{
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>
    /// 是否已确认固定到资源管理器快速访问。由快速访问同步标记；
    /// 用户在资源管理器里取消固定后，下次同步会把这类条目从启动器移除。
    /// </summary>
    public bool QuickAccessPinned { get; set; }
}

/// <summary>网站快捷方式（WebAtomList）。</summary>
public sealed class WebItem
{
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}