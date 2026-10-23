using DevEnv.Core.NetworkWatch.Models;

namespace DevEnv.Tests;

public sealed class SpeedTestResultTests
{
    [Fact]
    public void TextProperties_ReuseTrafficFormatter()
    {
        using var _ = new InvariantCultureScope();

        var result = new SpeedTestResult
        {
            DownloadBytesPerSecond = 1572864,
            UploadBytesPerSecond = 512,
            PingMs = 12.4,
            JitterMs = 3.6,
            TotalSeconds = 8.25,
        };

        Assert.Equal("1.5 MB/s", result.DownloadText);
        Assert.Equal("512 B/s", result.UploadText);
        Assert.Equal("12 ms", result.PingText);
        Assert.Equal("4 ms", result.JitterText);
        Assert.Equal("8.3 秒", result.DurationText);
    }

    [Fact]
    public void ServerName_DefaultsToCloudflare()
    {
        var result = new SpeedTestResult { DownloadBytesPerSecond = 1, UploadBytesPerSecond = 1 };

        Assert.Equal("Cloudflare", result.ServerName);
    }
}