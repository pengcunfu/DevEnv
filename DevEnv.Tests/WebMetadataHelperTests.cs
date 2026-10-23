using DevEnv.Core;

namespace DevEnv.Tests;

public sealed class WebMetadataHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeUrl_Blank_ReturnsNull(string? input)
    {
        Assert.Null(WebMetadataHelper.NormalizeUrl(input!));
    }

    [Theory]
    [InlineData("example.com", "https://example.com")]
    [InlineData("  example.com  ", "https://example.com")]
    [InlineData("example.com/path?q=1", "https://example.com/path?q=1")]
    [InlineData("HTTPS://Example.com", "https://example.com")]
    public void NormalizeUrl_AddsSchemeAndStripsTrailingSlash(string input, string expected)
    {
        Assert.Equal(expected, WebMetadataHelper.NormalizeUrl(input));
    }

    [Fact]
    public void NormalizeUrl_KeepsExplicitHttpAndHttps()
    {
        Assert.Equal("http://example.com", WebMetadataHelper.NormalizeUrl("http://example.com/"));
        Assert.Equal("https://example.com", WebMetadataHelper.NormalizeUrl("https://example.com"));
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///c:/windows")]
    public void NormalizeUrl_RejectsNonHttpSchemes(string input)
    {
        Assert.Null(WebMetadataHelper.NormalizeUrl(input));
    }
}