namespace DevEnv.Core.FileTools.Models;

/// <summary>粉碎参数。</summary>
public sealed record ShredOptions
{
    /// <summary>覆盖遍数（1~35）。</summary>
    public int Passes { get; init; } = 3;

    /// <summary>覆盖完成后删除文件；测试里可设为 false 只验证覆盖。</summary>
    public bool DeleteAfterShred { get; init; } = true;

    /// <summary>删除前把文件改成随机名称，避免原文件名残留在目录项里。</summary>
    public bool RandomizeName { get; init; } = true;
}

/// <summary>粉碎进度快照（由后台线程通过 Progress&lt;T&gt; 投递到界面线程）。</summary>
public sealed record ShredProgress
{
    public string CurrentPath { get; init; } = string.Empty;
    public int FileIndex { get; init; }
    public int FileCount { get; init; }
    public int Pass { get; init; }
    public int Passes { get; init; }

    /// <summary>0~100。总工作量按「文件大小 × 覆盖遍数」计算。</summary>
    public double Percent { get; init; }
}

/// <summary>单个路径的失败信息。</summary>
public sealed record ShredFailure(string Path, string Error);

/// <summary>一次粉碎任务的结果。</summary>
public sealed class ShredResult
{
    /// <summary>实际处理的文件数（含符号链接本身，不含目录）。</summary>
    public int TotalFiles { get; init; }

    public int ShreddedFiles { get; set; }

    public int FailedFiles => Failures.Count;

    /// <summary>已写入随机数据的字节数（文件大小 × 覆盖遍数）。</summary>
    public long OverwrittenBytes { get; set; }

    public int RemovedDirectories { get; set; }

    public bool Canceled { get; set; }

    /// <summary>路径级失败：单个文件失败不会中断整批任务。</summary>
    public List<ShredFailure> Failures { get; } = [];

    /// <summary>非致命提示，例如目录非空未能删除。</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>整批任务无法开始时的错误（例如没有路径）。</summary>
    public string? FatalError { get; init; }

    public bool Ok => FatalError is null && Failures.Count == 0;
}
