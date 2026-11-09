namespace DevEnv.Core.Models;

/// <summary>主程序设置项（data.json 的 Config 段）。</summary>
public sealed class AppConfig
{
    public bool TrayIcon { get; set; } = true;
    public bool SelfStarting { get; set; }
    public string BaseEnvPath { get; set; } = string.Empty;
    public string Language { get; set; } = "zh-CN";
    public bool DarkMode { get; set; }
    public string ShowWindowHotkey { get; set; } = "Ctrl+Alt+D";

    /// <summary>全局唤起熔岩剪贴板。</summary>
    public string ClipboardHotkey { get; set; } = "Ctrl+Space";

    /// <summary>全局唤起熔岩翻译。</summary>
    public string TranslatorHotkey { get; set; } = "Alt+Space";

    /// <summary>单独按 Win 键打开全局启动器（默认开启）；关闭后 Win 键回到系统开始菜单。</summary>
    public bool AltKeyLauncher { get; set; } = true;
}

/// <summary>
/// 开发环境服务配置（data.json 的 DevServiceList 一项）：
/// 用户自己填写的后台进程，启动命令走 <c>cmd /c</c> 执行。
/// </summary>
public sealed class DevServiceConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string StartCommand { get; set; } = string.Empty;
    /// <summary>可选：优雅停止命令，如 <c>redis-cli shutdown</c>；留空则直接结束进程。</summary>
    public string StopCommand { get; set; } = string.Empty;
    /// <summary>可选：进程名（不含 .exe），用于识别运行状态，并在重启本程序后仍能停止它。</summary>
    public string ProcessName { get; set; } = string.Empty;
}

/// <summary>开发环境服务列表中的一行（配置 + 实时状态）。</summary>
public sealed class DevServiceItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string WorkingDirectory { get; init; } = string.Empty;
    public string StartCommand { get; init; } = string.Empty;
    public string StopCommand { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public bool Running { get; init; }
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
    /// <summary>兼容旧命名：当前安装是否支持自更新（便携版可写目录为 true）。</summary>
    public bool IsInstalled { get; init; }
    public bool CanUpdate { get; init; }
    public bool HasBackup { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string? PendingVersion { get; init; }
    public bool Downloading { get; init; }
    public bool ReadyToApply { get; init; }
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

/// <summary>剪贴板保留窗口内的统计（总条数 / 文本 / 图片）。</summary>
public sealed class ClipboardStatsView
{
    public int Total { get; init; }
    public int Text { get; init; }
    public int Image { get; init; }
    public int RetentionDays { get; init; }
}

/// <summary>剪贴板历史一条（包装 DevEnv.Core.Clipboard 模型并补上图片绝对路径）。</summary>
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