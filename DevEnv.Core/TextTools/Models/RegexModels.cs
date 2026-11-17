using System.Globalization;

namespace DevEnv.Core.TextTools.Models;

/// <summary>一次正则匹配的命中信息，位置与长度均以字符为单位。</summary>
public sealed record RegexMatchItem(int Index, int Length, string Value, IReadOnlyList<string> Groups)
{
    public string IndexText => Index.ToString(CultureInfo.InvariantCulture);

    public string LengthText => Length.ToString(CultureInfo.InvariantCulture);

    public string GroupsText => Groups.Count == 0 ? string.Empty : string.Join("  ", Groups);
}

/// <summary>正则测试结果；表达式无效或匹配超时时 <see cref="Error"/> 给出原因。</summary>
public sealed record RegexTestResult(
    bool Ok,
    string Error,
    IReadOnlyList<RegexMatchItem> Matches,
    long ElapsedMs,
    bool Truncated)
{
    public int Count => Matches.Count;

    public static RegexTestResult Fail(string error) => new(false, error, [], 0, false);
}
