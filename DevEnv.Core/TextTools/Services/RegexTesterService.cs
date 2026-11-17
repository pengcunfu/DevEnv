using System.Diagnostics;
using System.Text.RegularExpressions;
using DevEnv.Core.TextTools.Models;

namespace DevEnv.Core.TextTools.Services;

/// <summary>
/// 正则测试：使用 .NET 正则引擎执行匹配，返回每处命中的位置、长度、文本与捕获组。
/// 带 2 秒超时，避免灾难性回溯把界面卡死；结果超过上限时截断并标记。
/// </summary>
public sealed class RegexTesterService
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    public const int DefaultMaxMatches = 2000;

    public RegexTestResult Test(
        string? pattern,
        string? input,
        RegexOptions options = RegexOptions.None,
        int maxMatches = DefaultMaxMatches)
    {
        if (string.IsNullOrEmpty(pattern))
            return RegexTestResult.Fail("请输入正则表达式");

        if (maxMatches <= 0)
            maxMatches = DefaultMaxMatches;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var regex = new Regex(pattern, options, MatchTimeout);
            var text = input ?? string.Empty;
            var matches = new List<RegexMatchItem>();
            var truncated = false;

            foreach (Match match in regex.Matches(text))
            {
                if (matches.Count >= maxMatches)
                {
                    truncated = true;
                    break;
                }

                matches.Add(new RegexMatchItem(
                    match.Index,
                    match.Length,
                    match.Value,
                    CollectGroups(regex, match)));
            }

            stopwatch.Stop();
            return new RegexTestResult(true, string.Empty, matches, stopwatch.ElapsedMilliseconds, truncated);
        }
        catch (ArgumentException ex)
        {
            return RegexTestResult.Fail($"正则表达式无效：{ex.Message}");
        }
        catch (RegexMatchTimeoutException)
        {
            return RegexTestResult.Fail("匹配超时（超过 2 秒），请简化表达式或减少测试文本");
        }
        catch (Exception ex)
        {
            return RegexTestResult.Fail(ex.Message);
        }
    }

    private static List<string> CollectGroups(Regex regex, Match match)
    {
        var groups = new List<string>();
        foreach (var groupName in regex.GetGroupNames())
        {
            if (groupName == "0")
                continue;

            var group = match.Groups[groupName];
            if (!group.Success)
                continue;

            groups.Add($"{groupName}: {group.Value}");
        }

        return groups;
    }
}
