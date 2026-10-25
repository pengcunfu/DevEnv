using DevEnv.Core.LavaEnv.Models;
using DevEnv.Core.LavaEnv.Services;

namespace DevEnv.Tests;

public sealed class HostsFileValidatorTests
{
    [Fact]
    public void Validate_ValidFile_ReturnsEntriesWithoutIssues()
    {
        const string text = """
            # 默认 hosts
            127.0.0.1 localhost
            ::1 localhost
            10.0.0.1 dev.local api.dev.local # 行内注释
            """;

        var result = HostsFileValidator.Validate(text);

        Assert.Empty(result.Issues);
        Assert.Equal(3, result.EntryCount);
        Assert.Equal("localhost", Assert.Single(result.Entries[0].HostNames));
        Assert.Equal("::1", result.Entries[1].Address);
        Assert.Equal(new[] { "dev.local", "api.dev.local" }, result.Entries[2].HostNames);
    }

    [Fact]
    public void Validate_EmptyOrCommentOnly_ReturnsEmptyResult()
    {
        var result = HostsFileValidator.Validate("""

            # 只有注释
                # 缩进注释

            """);

        Assert.Empty(result.Issues);
        Assert.Equal(0, result.EntryCount);
    }

    [Theory]
    [InlineData("300.1.1.1 foo")]
    [InlineData("1.2.3 foo")]
    [InlineData("not-an-ip foo")]
    public void Validate_InvalidAddress_ReportsError(string line)
    {
        var result = HostsFileValidator.Validate(line);

        var issue = Assert.Single(result.Issues);
        Assert.True(issue.IsError);
        Assert.Contains("IP 地址", issue.Message);
    }

    [Fact]
    public void Validate_AddressWithoutHost_ReportsError()
    {
        var issue = Assert.Single(HostsFileValidator.Validate("127.0.0.1").Issues);

        Assert.True(issue.IsError);
        Assert.Contains("缺少主机名", issue.Message);
    }

    [Fact]
    public void Validate_UnrecognizedSingleToken_ReportsError()
    {
        var issue = Assert.Single(HostsFileValidator.Validate("localhost").Issues);

        Assert.True(issue.IsError);
        Assert.Contains("无法识别", issue.Message);
    }

    [Theory]
    [InlineData("127.0.0.1 bad$host", "非法字符")]
    [InlineData("127.0.0.1 *.example.com", "通配符")]
    [InlineData("127.0.0.1 -bad.example.com", "连字符")]
    [InlineData("127.0.0.1 bad..example.com", "空标签")]
    public void Validate_InvalidHostName_ReportsError(string line, string expected)
    {
        var issue = Assert.Single(HostsFileValidator.Validate(line).Issues);

        Assert.True(issue.IsError);
        Assert.Contains(expected, issue.Message);
    }

    [Fact]
    public void Validate_ConflictingMapping_ReportsWarning()
    {
        var result = HostsFileValidator.Validate("""
            127.0.0.1 dev.local
            10.0.0.1 dev.local
            """);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(HostsIssueSeverity.Warning, issue.Severity);
        Assert.Equal(2, issue.LineNumber);
        Assert.Contains("此前映射到 127.0.0.1", issue.Message);
    }

    [Fact]
    public void Validate_DualStackLocalhost_DoesNotWarn()
    {
        var result = HostsFileValidator.Validate("""
            127.0.0.1 localhost
            ::1 localhost
            """);

        Assert.Empty(result.Issues);
        Assert.Equal(2, result.EntryCount);
    }

    [Fact]
    public void Validate_LocalhostMappedElsewhere_ReportsWarning()
    {
        var issue = Assert.Single(HostsFileValidator.Validate("10.0.0.1 localhost").Issues);

        Assert.Equal(HostsIssueSeverity.Warning, issue.Severity);
        Assert.Contains("localhost", issue.Message);
    }

    [Fact]
    public void Validate_DuplicateEntry_ReportsWarning()
    {
        var result = HostsFileValidator.Validate("""
            127.0.0.1 dev.local
            127.0.0.1 dev.local
            """);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(HostsIssueSeverity.Warning, issue.Severity);
        Assert.Contains("重复条目", issue.Message);
    }

    [Fact]
    public void Validate_UnderscoreHostName_ReportsWarning()
    {
        var issue = Assert.Single(HostsFileValidator.Validate("127.0.0.1 my_host").Issues);

        Assert.Equal(HostsIssueSeverity.Warning, issue.Severity);
        Assert.Contains("下划线", issue.Message);
    }
}
