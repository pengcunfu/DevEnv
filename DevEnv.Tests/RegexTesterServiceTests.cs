using System.Text.RegularExpressions;
using DevEnv.Core.TextTools.Services;

namespace DevEnv.Tests;

public sealed class RegexTesterServiceTests
{
    private readonly RegexTesterService _service = new();

    [Fact]
    public void Test_ReturnsMatchesWithPositionAndLength()
    {
        var result = _service.Test(@"\d+", "a12b345");

        Assert.True(result.Ok);
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result.Matches[0].Index);
        Assert.Equal(2, result.Matches[0].Length);
        Assert.Equal("12", result.Matches[0].Value);
        Assert.Equal("345", result.Matches[1].Value);
    }

    [Fact]
    public void Test_CollectsNamedAndNumberedGroups()
    {
        var result = _service.Test(@"(?<year>\d{4})-(\d{2})", "2026-11");

        var match = Assert.Single(result.Matches);
        Assert.Equal("2026-11", match.Value);
        Assert.Contains(match.Groups, group => group.EndsWith("2026", StringComparison.Ordinal));
        Assert.Contains(match.Groups, group => group.EndsWith("11", StringComparison.Ordinal));
    }

    [Fact]
    public void Test_IgnoreCaseOptionApplies()
    {
        var withoutOption = _service.Test("fireneb", "FireNeb");
        var withOption = _service.Test("fireneb", "FireNeb", RegexOptions.IgnoreCase);

        Assert.Empty(withoutOption.Matches);
        Assert.Single(withOption.Matches);
    }

    [Fact]
    public void Test_InvalidPatternReportsError()
    {
        var result = _service.Test("([", "abc");

        Assert.False(result.Ok);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Test_TruncatesWhenOverLimit()
    {
        var result = _service.Test("a", "aaaaa", maxMatches: 3);

        Assert.True(result.Ok);
        Assert.Equal(3, result.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public void Test_EmptyPatternReportsError()
    {
        var result = _service.Test(string.Empty, "abc");

        Assert.False(result.Ok);
        Assert.Contains("请输入", result.Error, StringComparison.Ordinal);
    }
}
