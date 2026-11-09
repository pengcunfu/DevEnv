using DevEnv.Core.Services;

namespace DevEnv.Tests;

public sealed class StartMenuNamingTests
{
    [Theory]
    [InlineData("Demo:App?", "DemoApp")]
    [InlineData("  Demo  ", "Demo")]
    [InlineData("...", "应用")]
    [InlineData("", "应用")]
    [InlineData(null, "应用")]
    public void SanitizeFileName_RemovesInvalidCharacters(string? input, string expected)
    {
        Assert.Equal(expected, StartMenuNaming.SanitizeFileName(input));
    }

    [Fact]
    public void SanitizeFileName_TrimsToMaxLength()
    {
        var name = new string('a', StartMenuNaming.MaxBaseNameLength + 20);

        Assert.Equal(StartMenuNaming.MaxBaseNameLength, StartMenuNaming.SanitizeFileName(name).Length);
    }

    [Fact]
    public void MakeUniqueFileName_ReturnsUnknownNameAsIs()
    {
        Assert.Equal("Demo.lnk", StartMenuNaming.MakeUniqueFileName("Demo", ".lnk", []));
    }

    [Fact]
    public void MakeUniqueFileName_AppendsSuffixCaseInsensitively()
    {
        Assert.Equal("Demo (2).lnk", StartMenuNaming.MakeUniqueFileName("Demo", ".lnk", ["demo.lnk"]));
        Assert.Equal("Demo (3).lnk",
            StartMenuNaming.MakeUniqueFileName("Demo", ".lnk", ["Demo.lnk", "Demo (2).lnk"]));
    }

    [Fact]
    public void MakeUniqueFileName_AcceptsExtensionWithoutDot()
    {
        Assert.Equal("Demo.lnk", StartMenuNaming.MakeUniqueFileName("Demo", "lnk", ["Other.lnk"]));
    }
}
