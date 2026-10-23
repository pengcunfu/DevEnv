namespace DevBox.Core.Models;

/// <summary>本地应用快捷方式（data.json 的 NativeAtomList）。</summary>
public sealed class NativeAppItem
{
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExecPath { get; set; } = string.Empty;
}

/// <summary>文件夹快捷方式（FolderAtomList）。</summary>
public sealed class FolderItem
{
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
}

/// <summary>网站快捷方式（WebAtomList）。</summary>
public sealed class WebItem
{
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}