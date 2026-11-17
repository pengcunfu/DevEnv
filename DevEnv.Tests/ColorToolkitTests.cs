using DevEnv.Core.ColorTools.Models;
using DevEnv.Core.ColorTools.Services;

namespace DevEnv.Tests;

public sealed class ColorToolkitTests
{
    [Theory]
    [InlineData("#3B82F6", 255, 59, 130, 246)]
    [InlineData("3b82f6", 255, 59, 130, 246)]
    [InlineData("#FFF", 255, 255, 255, 255)]
    [InlineData("#80FF0000", 128, 255, 0, 0)]
    [InlineData("rgb(59, 130, 246)", 255, 59, 130, 246)]
    [InlineData("rgba(59, 130, 246, 0.5)", 128, 59, 130, 246)]
    [InlineData("rgba(59, 130, 246, 1)", 255, 59, 130, 246)]
    [InlineData("hsl(0, 100%, 50%)", 255, 255, 0, 0)]
    [InlineData("hsl(0deg, 100%, 50%)", 255, 255, 0, 0)]
    public void TryParse_RecognizesSupportedFormats(string text, byte a, byte r, byte g, byte b)
    {
        Assert.True(ColorToolkit.TryParse(text, out var color, out var error), error);
        Assert.Equal(a, color.A);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#GGGGGG")]
    [InlineData("rgb(1,2)")]
    [InlineData("not-a-color")]
    public void TryParse_RejectsInvalidInput(string text)
    {
        Assert.False(ColorToolkit.TryParse(text, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Hex_IncludesAlphaOnlyWhenTranslucent()
    {
        Assert.Equal("#3B82F6", new ColorValue(255, 59, 130, 246).Hex);
        Assert.Equal("#803B82F6", new ColorValue(128, 59, 130, 246).Hex);
    }

    [Fact]
    public void FromHsl_And_ToHsl_RoundTripPrimaryColours()
    {
        var red = ColorToolkit.FromHsl(0, 100, 50);
        var (h, s, l) = ColorToolkit.ToHsl(red);

        Assert.Equal(255, red.R);
        Assert.Equal(0, red.G);
        Assert.Equal(0, red.B);
        Assert.Equal(0, h, 3);
        Assert.Equal(100, s, 3);
        Assert.Equal(50, l, 3);
    }

    [Fact]
    public void FromHsl_WrapsHueAndClamps()
    {
        var wrapped = ColorToolkit.FromHsl(360, 100, 50);
        var clamped = ColorToolkit.FromHsl(0, 200, 150);

        Assert.Equal(new ColorValue(255, 255, 0, 0), wrapped);
        Assert.Equal(new ColorValue(255, 255, 255, 255), clamped);
    }

    [Fact]
    public void GetPalette_ReturnsOrderedLightToDark()
    {
        var palette = ColorToolkit.GetPalette(new ColorValue(255, 59, 130, 246), 5);

        Assert.Equal(5, palette.Count);
        var first = ColorToolkit.ToHsl(palette[0]).L;
        var last = ColorToolkit.ToHsl(palette[^1]).L;
        Assert.True(first > last);
    }

    [Fact]
    public void HslText_IsInvariantAndReadable()
    {
        using var _ = new InvariantCultureScope();
        var text = ColorToolkit.HslText(new ColorValue(255, 59, 130, 246));

        Assert.StartsWith("hsl(", text, StringComparison.Ordinal);
        Assert.Contains("%", text, StringComparison.Ordinal);
    }
}
