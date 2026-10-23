using DevEnv.Core.LavaEnv.Models;

namespace DevEnv.Tests;

public sealed class MirrorSourceTests
{
    [Fact]
    public void SpeedTestUrl_FallsBackToUrl()
    {
        var source = new MirrorSource { Name = "官方", Url = "https://example.com/pkg" };

        Assert.Equal("https://example.com/pkg", source.SpeedTestUrl);

        source.TestUrl = "   ";
        Assert.Equal("https://example.com/pkg", source.SpeedTestUrl);
    }

    [Fact]
    public void SpeedTestUrl_PrefersExplicitTestUrl()
    {
        var source = new MirrorSource { Url = "https://example.com/pkg", TestUrl = "https://cdn.example.com/pkg.zip" };

        Assert.Equal("https://cdn.example.com/pkg.zip", source.SpeedTestUrl);
    }

    [Fact]
    public void DisplayName_WithoutLatency_IsPlainName()
    {
        var source = new MirrorSource { Name = "清华镜像" };

        Assert.Equal("清华镜像", source.DisplayName);
    }

    [Fact]
    public void DisplayName_WithLatency_AppendsMilliseconds()
    {
        using var _ = new InvariantCultureScope();
        var source = new MirrorSource { Name = "清华镜像", ResponseTimeMs = 123.6 };

        Assert.Equal("清华镜像 - 124 ms", source.DisplayName);
    }
}