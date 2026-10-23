namespace DevBox.Core.Models;

/// <summary>主程序设置项（data.json 的 Config 段）。</summary>
public sealed class AppConfig
{
    public bool TrayIcon { get; set; } = true;
    public bool SelfStarting { get; set; }
    public string BaseEnvPath { get; set; } = string.Empty;
    public string Language { get; set; } = "zh-CN";
    public bool DarkMode { get; set; }
    public bool DoubleClickToLaunch { get; set; }
    public string ShowWindowHotkey { get; set; } = "Ctrl+Alt+D";
}

/// <summary>开发环境服务（MySQL / Redis / PostgreSQL）一行。</summary>
public sealed class DevServiceItem
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string InstallPath { get; init; }
    public required string DataPath { get; init; }
    public bool Running { get; init; }
    public bool Installed { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public bool CanStart { get; init; }
    public bool CanStop { get; init; }
}

/// <summary>开机启动项（注册表 Run 键 / 启动文件夹）。</summary>
public sealed class StartupEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required string Location { get; init; }
    public bool Enabled { get; init; } = true;
}

/// <summary>Windows 系统工具目录项。</summary>
public sealed class ToolInfo
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    /// <summary>图标文件绝对路径（宿主直接绑定 Image.Source）。</summary>
    public string IconPath { get; init; } = string.Empty;
}

/// <summary>自更新状态。</summary>
public sealed class UpdateStatusView
{
    public bool IsInstalled { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string? PendingVersion { get; init; }
    public bool Downloading { get; init; }
    public int DownloadProgress { get; init; }
    public string? Error { get; init; }
}

/// <summary>翻译站点。</summary>
public sealed class TranslateSiteInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string HomeUrl { get; init; }
}

/// <summary>图片转换结果一行。</summary>
public sealed class ImageConvertResult
{
    public required string Input { get; init; }
    public string? Output { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public sealed class ImageConvertBatch
{
    public IReadOnlyList<ImageConvertResult> Results { get; init; } = [];
    public int SuccessCount { get; init; }
}

/// <summary>哈希计算结果。</summary>
public sealed class HashResultView
{
    public required string Hash { get; init; }
    public required string Algorithm { get; init; }
    public string FileSize { get; init; } = string.Empty;
    public double ElapsedMs { get; init; }
}

/// <summary>Hosts 文件内容。</summary>
public sealed class HostsFileView
{
    public required string Path { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool CanWrite { get; init; }
}

/// <summary>剪贴板历史一条（包装 ClipboardApp 模型并补上图片绝对路径）。</summary>
public sealed class ClipboardEntryView
{
    public required long Id { get; init; }
    public required string Timestamp { get; init; }
    public required string Type { get; init; }
    public string? Text { get; init; }
    public string? ImagePath { get; init; }
    /// <summary>图片在磁盘上的绝对路径（宿主直接绑定图片控件），非图片为 null。</summary>
    public string? ImageFullPath { get; init; }
    public string DisplayText { get; init; } = string.Empty;
    public bool IsImage { get; init; }
}