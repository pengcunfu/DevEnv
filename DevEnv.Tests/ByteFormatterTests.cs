using DevEnv.Core.ProcessWatch.Models;

namespace DevEnv.Tests;

public sealed class ByteFormatterTests
{
    [Fact]
    public void FormatBytes_Negative_ClampsToZero()
    {
        using var _ = new InvariantCultureScope();

        Assert.Equal("0 B", ByteFormatter.FormatBytes(-1));
        Assert.Equal("0 B", ByteFormatter.FormatBytes(long.MinValue));
    }

    [Fact]
    public void FormatBytes_BelowOneKib_KeepsRawNumber()
    {
        using var _ = new InvariantCultureScope();

        Assert.Equal("0 B", ByteFormatter.FormatBytes(0));
        Assert.Equal("1023 B", ByteFormatter.FormatBytes(1023));
    }

    [Theory]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1048576L, "1 MB")]
    public void FormatBytes_ScalesToUnits(long bytes, string expected)
    {
        using var _ = new InvariantCultureScope();

        Assert.Equal(expected, ByteFormatter.FormatBytes(bytes));
    }
}