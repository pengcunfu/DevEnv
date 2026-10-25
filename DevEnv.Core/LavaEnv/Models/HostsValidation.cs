namespace DevEnv.Core.LavaEnv.Models;

/// <summary>Hosts 校验问题级别。</summary>
public enum HostsIssueSeverity
{
    Warning,
    Error,
}

/// <summary>Hosts 校验中的一条问题，行号从 1 开始。</summary>
public sealed record HostsValidationIssue(int LineNumber, HostsIssueSeverity Severity, string Message)
{
    public bool IsError => Severity == HostsIssueSeverity.Error;

    public string LineLabel => $"第 {LineNumber} 行";
}

/// <summary>Hosts 中一条有效记录：一个地址 + 一个或多个主机名。</summary>
public sealed record HostsFileEntry(int LineNumber, string Address, IReadOnlyList<string> HostNames);

/// <summary>Hosts 文本校验结果；只含纯数据，不依赖界面。</summary>
public sealed class HostsValidationResult
{
    public HostsValidationResult(
        IReadOnlyList<HostsValidationIssue> issues,
        IReadOnlyList<HostsFileEntry> entries)
    {
        Issues = issues;
        Entries = entries;
    }

    public IReadOnlyList<HostsValidationIssue> Issues { get; }

    public IReadOnlyList<HostsFileEntry> Entries { get; }

    public int ErrorCount => Issues.Count(issue => issue.IsError);

    public int WarningCount => Issues.Count - ErrorCount;

    public bool HasErrors => ErrorCount > 0;

    public int EntryCount => Entries.Count;
}
