using DevEnv.Core.SigXor;

namespace DevEnv.Tests.SigXor;

public sealed class VersionTextTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("2", 2, 0, 0)]
    [InlineData("1.2.3-beta.1+build", 1, 2, 3)]
    [InlineData("0.0.0-2610241200", 0, 0, 0)]
    public void Parse_Normalizes_Numeric_Parts(string text, int major, int minor, int patch)
    {
        Assert.True(VersionText.TryParse(text, out var version));
        Assert.Equal(new VersionText(major, minor, patch), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1..2")]
    [InlineData("-1.2.3")]
    [InlineData("1.2.3.4")]
    [InlineData("v1.2.3")]
    [InlineData("1/2/3")]
    public void Parse_Invalid_Version_Returns_False(string? text)
        => Assert.False(VersionText.TryParse(text, out _));

    [Theory]
    [InlineData("1.10.0", "1.9.9", 1)]
    [InlineData("2.0.0", "1.99.99", 1)]
    [InlineData("1.2.0-beta.1", "1.2", 0)]
    [InlineData("1.2.2", "1.2.3", -1)]
    public void Compare_Uses_Numbers_And_Ignores_Metadata(string left, string right, int expected)
    {
        Assert.True(VersionText.TryParse(left, out var a));
        Assert.True(VersionText.TryParse(right, out var b));
        Assert.Equal(expected, Math.Sign(a.CompareTo(b)));
    }
}
