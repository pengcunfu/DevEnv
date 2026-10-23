using DevEnv.Core;

namespace DevEnv.Tests;

public sealed class IconHelperTests
{
    [Fact]
    public void ComputeHash_IsStableAndCaseInsensitive()
    {
        var first = IconHelper.ComputeHash(@"C:\Tools\Code.exe");
        var second = IconHelper.ComputeHash(@"c:\tools\code.exe");

        Assert.Equal(first, second);
        Assert.Equal(16, first.Length);
        Assert.All(first, c => Assert.True(Uri.IsHexDigit(c)));
        Assert.Equal(first.ToLowerInvariant(), first);
    }

    [Fact]
    public void ComputeHash_DifferentKeysDiffer()
    {
        Assert.NotEqual(IconHelper.ComputeHash("a"), IconHelper.ComputeHash("b"));
    }

    [Fact]
    public void IsUnderIconsDir_MatchesOnlyIconsDirectory()
    {
        var inside = Path.Combine(IconHelper.IconsDirectory, "exe_0123456789abcdef.png");
        var outside = Path.Combine(Path.GetTempPath(), "devenv-outside.png");

        Assert.True(IconHelper.IsUnderIconsDir(inside));
        Assert.False(IconHelper.IsUnderIconsDir(outside));
    }
}