using DevEnv.Core.Translator.Services;

namespace DevEnv.Tests;

public sealed class TranslatorConfigServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "devenv-translator-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // 清理失败不影响测试结果
        }
    }

    [Fact]
    public void Constructor_CreatesDefaultConfigInGivenDirectory()
    {
        var service = new ConfigService(_dir);

        Assert.Equal(Path.Combine(_dir, "config.json"), service.ConfigFilePath);
        Assert.True(File.Exists(service.ConfigFilePath));
        Assert.Equal("youdao", service.Current.General.LastWebSiteId);
    }

    [Fact]
    public void SaveAndReload_PersistsLastSite()
    {
        var service = new ConfigService(_dir);
        var general = service.Current.General;
        general.LastWebSiteId = "deepl";
        Assert.True(service.Save());

        var reloaded = new ConfigService(_dir);
        Assert.Equal("deepl", reloaded.Current.General.LastWebSiteId);
    }
}
