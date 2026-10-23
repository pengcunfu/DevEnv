using DevEnv.Core.NetworkWatch.Models;

namespace DevEnv.Tests;

public sealed class TrafficFormatterTests
{
    [Fact]
    public void FormatBytes_BelowOneKib_KeepsRawNumber()
    {
        using var _ = new InvariantCultureScope();

        Assert.Equal("0 B", TrafficFormatter.FormatBytes(0));
        Assert.Equal("512 B", TrafficFormatter.FormatBytes(512));
        Assert.Equal("1023 B", TrafficFormatter.FormatBytes(1023));
    }

    [Theory]
    [InlineData(1024UL, "1 KB")]
    [InlineData(1536UL, "1.5 KB")]
    [InlineData(1048576UL, "1 MB")]
    [InlineData(1073741824UL, "1 GB")]
    public void FormatBytes_ScalesToUnits(ulong bytes, string expected)
    {
        using var _ = new InvariantCultureScope();

        Assert.Equal(expected, TrafficFormatter.FormatBytes(bytes));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void FormatRate_NonPositive_ReturnsZero(double value)
    {
        using var _ = new InvariantCultureScope();

        Assert.Equal("0 B/s", TrafficFormatter.FormatRate(value));
    }

    [Theory]
    [InlineData(512d, "512 B/s")]
    [InlineData(2048d, "2 KB/s")]
    [InlineData(1572864d, "1.5 MB/s")]
    public void FormatRate_ScalesToUnits(double value, string expected)
    {
        using var _ = new InvariantCultureScope();

        Assert.Equal(expected, TrafficFormatter.FormatRate(value));
    }
}