namespace DevEnv.Core.FileTools.Models;

public enum RenameCaseMode
{
    Keep,
    Upper,
    Lower,
}

/// <summary>批量重命名规则：先查找替换，再转换大小写，最后拼前后缀与序号。</summary>
public sealed class BatchRenameOptions
{
    public string Prefix { get; set; } = string.Empty;

    public string Suffix { get; set; } = string.Empty;

    public string Find { get; set; } = string.Empty;

    public string Replace { get; set; } = string.Empty;

    public bool UseNumbering { get; set; }

    public int StartNumber { get; set; } = 1;

    public int Step { get; set; } = 1;

    public int NumberWidth { get; set; } = 2;

    public string NumberSeparator { get; set; } = "_";

    public RenameCaseMode CaseMode { get; set; } = RenameCaseMode.Keep;

    /// <summary>为 true 时查找替换与大小写转换也作用于扩展名。</summary>
    public bool IncludeExtension { get; set; }
}

/// <summary>单个文件的重命名预览。</summary>
public sealed record BatchRenameItem(
    string SourcePath,
    string OriginalName,
    string NewName,
    bool Changed,
    string? Error)
{
    public bool HasError => Error is not null;

    public string StatusText => Error ?? (Changed ? "将重命名" : "不变");
}

/// <summary>整个文件夹的重命名预览结果。</summary>
public sealed record BatchRenamePreview(
    IReadOnlyList<BatchRenameItem> Items,
    int ChangedCount,
    int ErrorCount)
{
    public int TotalCount => Items.Count;
}
