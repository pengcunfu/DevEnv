namespace DevEnv.Core.Models;

/// <summary>系统里已注册的右键菜单项（shell 命令项或 shellex COM 扩展）。</summary>
public sealed class SystemContextMenuItem
{
    /// <summary>稳定标识：种类 + 作用域 + 键名。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>菜单显示名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>注册表子键名。</summary>
    public string KeyName { get; set; } = string.Empty;

    /// <summary>作用域路径，如 <c>*</c>、<c>Directory</c>、<c>DesktopBackground</c>。</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>作用域可读名称：文件 / 文件夹 / 文件夹空白处 / 桌面 / 文件与文件夹。</summary>
    public string ScopeText { get; set; } = string.Empty;

    /// <summary>类型：命令 / COM 扩展。</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>实际来源：HKCU / HKLM。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>完整注册表路径，供展示与复制。</summary>
    public string RegistryPath { get; set; } = string.Empty;

    /// <summary>菜单命令（COM 扩展时为 DLL 路径或 CLSID）。</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>菜单图标路径。</summary>
    public string IconPath { get; set; } = string.Empty;

    /// <summary>COM 扩展的 CLSID。</summary>
    public string Clsid { get; set; } = string.Empty;

    /// <summary>COM 扩展的 InprocServer32 DLL 路径。</summary>
    public string DllPath { get; set; } = string.Empty;

    /// <summary>当前是否出现在右键菜单里。</summary>
    public bool Enabled { get; set; }

    /// <summary>能否通过当前用户注册表切换启用状态（系统隐藏或系统策略项为 false）。</summary>
    public bool CanToggle { get; set; }

    /// <summary>补充说明：系统隐藏、Win11 命令、Shift 扩展菜单等。</summary>
    public string Note { get; set; } = string.Empty;

    public string StatusText => Enabled ? "已启用" : CanToggle ? "已禁用" : "不可切换";
}
