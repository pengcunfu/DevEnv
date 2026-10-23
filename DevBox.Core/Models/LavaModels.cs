namespace DevBox.Core.Models;

/// <summary>熔岩绿色版进程（服务页一行）。</summary>
public sealed class LavaProcessItem
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public required string StatusText { get; init; }
    public required string StatusColor { get; init; }
    public int? ProcessId { get; init; }
    public bool CanStart { get; init; }
    public bool CanStop { get; init; }
    public bool NeedsInstall { get; init; }
}

public sealed class LavaSettingsInfo
{
    public bool AutoExtractPortable { get; init; }
    public int MaxRetries { get; init; }
    public int Timeout { get; init; }
    public int ChunkSizeMb { get; init; }
    public bool UseSystemProxy { get; init; }
    public string CustomProxy { get; init; } = string.Empty;
}

public sealed class LavaAboutInfo
{
    public string ProductName { get; init; } = string.Empty;
    public string ProductShortName { get; init; } = string.Empty;
    public string Tagline { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string DocsUrl { get; init; } = string.Empty;
    public string RepositoryUrl { get; init; } = string.Empty;
}

/// <summary>熔岩数据目录页（路径 + 当前配置 + 关于）。</summary>
public sealed class LavaDataInfo
{
    public string Root { get; init; } = string.Empty;
    public string ConfigFile { get; init; } = string.Empty;
    public string CacheDir { get; init; } = string.Empty;
    public string AppsDir { get; init; } = string.Empty;
    public string HistoryFile { get; init; } = string.Empty;
    public LavaSettingsInfo Settings { get; init; } = new();
    public LavaAboutInfo About { get; init; } = new();
}

/// <summary>下载记录（历史页 / 活动页共用）。</summary>
public sealed class DownloadItem
{
    public required string Id { get; init; }
    public required string SoftwareName { get; init; }
    public required string Version { get; init; }
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required string Url { get; init; }
    public required string Status { get; init; }
    public required string StatusText { get; init; }
    public int Progress { get; init; }
    public string SpeedText { get; init; } = string.Empty;
    public string SizeText { get; init; } = string.Empty;
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public bool CanOpen { get; init; }
    public bool IsActive { get; init; }
}

public sealed class DownloadStats
{
    public int Total { get; init; }
    public int Completed { get; init; }
    public int Failed { get; init; }
    public int Downloading { get; init; }
    public long TotalBytes { get; init; }
    public string TotalBytesText { get; init; } = string.Empty;
}

public sealed class DownloadListResult
{
    public IReadOnlyList<DownloadItem> Items { get; init; } = [];
    public DownloadStats Stats { get; init; } = new();
}

public sealed class CatalogVersion
{
    public required string Version { get; init; }
    public required string Url { get; init; }
    public required string FileName { get; init; }
}

public sealed class CatalogItem
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Icon { get; init; }
    public IReadOnlyList<CatalogVersion> Versions { get; init; } = [];
}

public sealed class CatalogCategory
{
    public required string Category { get; init; }
    public IReadOnlyList<CatalogItem> Items { get; init; } = [];
}

public sealed class MirrorToolOption
{
    public required string Key { get; init; }
    public required string Label { get; init; }
}

public sealed class MirrorItem
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public double? ResponseTimeMs { get; init; }
}

public sealed class MirrorInfo
{
    public required string Tool { get; init; }
    public IReadOnlyList<MirrorToolOption> Tools { get; init; } = [];
    public string? Current { get; init; }
    public IReadOnlyList<MirrorItem> Mirrors { get; init; } = [];
}

public sealed class MirrorTestResult
{
    public IReadOnlyList<MirrorItem> Mirrors { get; init; } = [];
    public MirrorItem? Recommended { get; init; }
}