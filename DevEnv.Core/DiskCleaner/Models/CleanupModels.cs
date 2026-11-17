namespace DevEnv.Core.DiskCleaner.Models;

/// <summary>一个待清理的位置。带 <see cref="SearchPattern"/> 时只删除该目录下匹配的文件，不再递归。</summary>
public sealed class CleanupLocation
{
    public required string Path { get; init; }

    /// <summary>文件通配符（如 <c>thumbcache_*.db</c>）；为空表示清理整个目录内容。</summary>
    public string? SearchPattern { get; init; }
}

/// <summary>清理项定义：名称、分类、涉及的位置与是否需要管理员权限。</summary>
public sealed class CleanupTargetDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Category { get; init; }

    public string Description { get; init; } = string.Empty;

    public bool RequiresAdministrator { get; init; }

    /// <summary>扫描后默认是否勾选。会带来重新下载或首次启动变慢的项目默认不勾。</summary>
    public bool DefaultSelected { get; init; } = true;

    /// <summary>清空回收站走 Shell API，而不是遍历目录。</summary>
    public bool IsRecycleBin { get; init; }

    public IReadOnlyList<CleanupLocation> Locations { get; init; } = [];
}

/// <summary>单个清理项的扫描结果。</summary>
public sealed class CleanupScanItem
{
    public required CleanupTargetDefinition Definition { get; init; }

    public long SizeBytes { get; set; }

    public int FileCount { get; set; }

    /// <summary>实际存在的位置数量；为 0 表示没有找到该清理项。</summary>
    public int LocationCount { get; set; }

    /// <summary>至少有一个位置因权限等原因无法完整读取。</summary>
    public bool Accessible { get; set; } = true;

    public bool Exists => LocationCount > 0;

    public bool HasContent => SizeBytes > 0 || FileCount > 0;
}

/// <summary>一轮扫描的汇总。</summary>
public sealed class CleanupScanResult
{
    public List<CleanupScanItem> Items { get; } = [];

    public List<string> Warnings { get; } = [];

    public TimeSpan Elapsed { get; set; }

    public long TotalBytes => Items.Sum(i => i.SizeBytes);

    public int TotalFiles => Items.Sum(i => i.FileCount);
}

/// <summary>清理进度。比例按已处理的文件大小计算。</summary>
public sealed class CleanupProgress
{
    public string TargetName { get; init; } = string.Empty;

    public string CurrentPath { get; init; } = string.Empty;

    public long ProcessedBytes { get; init; }

    public long TotalBytes { get; init; }

    public int ProcessedFiles { get; init; }

    public int TotalFiles { get; init; }

    public double Percent => TotalBytes <= 0
        ? (TotalFiles <= 0 ? 0 : Math.Min(100, ProcessedFiles * 100.0 / TotalFiles))
        : Math.Min(100, ProcessedBytes * 100.0 / TotalBytes);
}

/// <summary>单个清理项的实际结果。</summary>
public sealed class CleanupTargetOutcome
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public long FreedBytes { get; set; }

    public int DeletedFiles { get; set; }

    public int DeletedDirectories { get; set; }

    public int SkippedFiles { get; set; }

    public bool RecycleBinEmptied { get; set; }

    public string? Error { get; set; }
}

/// <summary>一轮清理的汇总。</summary>
public sealed class CleanupResult
{
    public List<CleanupTargetOutcome> Outcomes { get; } = [];

    public List<string> Warnings { get; } = [];

    public bool Canceled { get; set; }

    public long TotalFreedBytes => Outcomes.Sum(o => o.FreedBytes);

    public int TotalDeletedFiles => Outcomes.Sum(o => o.DeletedFiles);

    public int TotalSkippedFiles => Outcomes.Sum(o => o.SkippedFiles);
}

/// <summary>C 盘容量信息。</summary>
public sealed class DiskSpaceInfo
{
    public bool Available { get; init; }

    public long TotalBytes { get; init; }

    public long FreeBytes { get; init; }

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double UsedPercent => TotalBytes <= 0 ? 0 : UsedBytes * 100.0 / TotalBytes;
}
