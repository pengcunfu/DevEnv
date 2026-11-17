using System.Globalization;

namespace DevEnv.Core.TextTools.Models;

public enum DiffLineKind
{
    Same,
    Added,
    Removed,
}

/// <summary>逐行差异中的一行；新增行没有旧行号，删除行没有新行号。</summary>
public sealed record DiffLine(DiffLineKind Kind, int OldLineNumber, int NewLineNumber, string Text)
{
    public string OldLineText => OldLineNumber > 0
        ? OldLineNumber.ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    public string NewLineText => NewLineNumber > 0
        ? NewLineNumber.ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    public string Sign => Kind switch
    {
        DiffLineKind.Added => "+",
        DiffLineKind.Removed => "-",
        _ => " ",
    };
}

/// <summary>两段文本的逐行对比结果。</summary>
public sealed record TextDiffResult(
    bool Ok,
    string Error,
    IReadOnlyList<DiffLine> Lines,
    int AddedCount,
    int RemovedCount)
{
    public static TextDiffResult Fail(string error) => new(false, error, [], 0, 0);
}
