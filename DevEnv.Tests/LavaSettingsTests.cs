using DevEnv.Core.LavaEnv.Services;

namespace DevEnv.Tests;

public sealed class LavaSettingsTests
{
    [Fact]
    public void CreateDefaultSettings_PointsAtLavaDirectories()
    {
        var settings = AppConfigService.CreateDefaultSettings();

        Assert.EndsWith("cache", settings.CacheDir, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("apps", settings.AppsDir, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDefaultSettings_UsesWorkingDefaults()
    {
        var settings = AppConfigService.CreateDefaultSettings();

        Assert.True(settings.MaxWorkers > 0, "并发数默认值应为正数，否则下载模块会空转");
        Assert.True(settings.ChunkSizeMb > 0, "分片大小默认值应为正数");
        Assert.True(settings.Timeout > 0, "超时默认值应为正数");
    }
}