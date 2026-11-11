using DevEnv.Core.LavaEnv.Models;

namespace DevEnv.Tests;

public sealed class HostsEntryTests
{
    [Theory]
    [InlineData("127.0.0.1", "localhost")]
    [InlineData("192.168.1.10", "nas.local")]
    [InlineData("0.0.0.0", "ads.example.com")]
    [InlineData("255.255.255.255", "broadcast")]
    [InlineData("::1", "localhost")]
    public void TryParse_ValidLine_ReturnsEntry(string ip, string domain)
    {
        Assert.True(HostsEntry.TryParse($"{ip} {domain}", out var entry));
        Assert.Equal(ip, entry.Ip);
        Assert.Equal(domain, entry.Domain);
    }

    [Fact]
    public void TryParse_KeepsAllDomains()
    {
        Assert.True(HostsEntry.TryParse("127.0.0.1  localhost alias.local", out var entry));
        Assert.Equal("localhost alias.local", entry.Domain);
    }

    [Fact]
    public void TryParse_TrimsWhitespaceAndStripsInlineComment()
    {
        Assert.True(HostsEntry.TryParse("   10.0.0.1\t  dev.local  # note ", out var entry));
        Assert.Equal("10.0.0.1", entry.Ip);
        Assert.Equal("dev.local", entry.Domain);
        Assert.Equal("10.0.0.1\t  dev.local", entry.OriginalLine);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# 注释")]
    [InlineData("    # 缩进注释")]
    [InlineData("localhost")]
    [InlineData("256.1.1.1 too-high")]
    [InlineData("1.2.3 too-few")]
    public void TryParse_InvalidLine_ReturnsFalse(string line)
    {
        Assert.False(HostsEntry.TryParse(line, out var entry));
        Assert.Null(entry);
    }

    [Fact]
    public void DisplayText_JoinsIpAndDomain()
    {
        Assert.True(HostsEntry.TryParse("127.0.0.1 localhost", out var entry));
        Assert.Equal("127.0.0.1\tlocalhost", entry.DisplayText);
    }
}
