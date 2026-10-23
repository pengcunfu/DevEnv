using DevEnv.Core.LavaEnv.Models;
using DevEnv.Core.LavaEnv.Services;

namespace DevEnv.Tests;

/// <summary>
/// 内置配置（随程序发布的 processes_config.json / software_config.json）必须始终能解析。
/// 这些文件直接决定熔岩页能否列出绿色版进程与可下载软件，坏掉时用户看到的是空列表而不是报错。
/// </summary>
public sealed class BundledConfigTests
{
    private static Dictionary<string, ProcessConfigEntry> LoadProcesses() =>
        ResourceLoader.LoadBundledJson<Dictionary<string, ProcessConfigEntry>>(ResourcePaths.ProcessesConfigFile)
        ?? throw new InvalidOperationException("processes_config.json 缺失或无法解析");

    private static Dictionary<string, List<SoftwareCatalogItem>> LoadSoftware() =>
        ResourceLoader.LoadBundledJson<Dictionary<string, List<SoftwareCatalogItem>>>(ResourcePaths.SoftwareCatalogFile)
        ?? throw new InvalidOperationException("software_config.json 缺失或无法解析");

    [Fact]
    public void LoadBundledJson_MissingFile_ReturnsDefault()
    {
        Assert.Null(ResourceLoader.LoadBundledJson<Dictionary<string, string>>("no-such-config.json"));
    }

    [Fact]
    public void ProcessesConfig_ParsesToNonEmptyDictionary()
    {
        var config = LoadProcesses();

        Assert.NotEmpty(config);
        Assert.All(config, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Key)));
    }

    [Fact]
    public void ProcessesConfig_EveryEntryIsUsable()
    {
        foreach (var (key, entry) in LoadProcesses())
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.DisplayName), $"{key} 缺少 display_name");
            Assert.False(string.IsNullOrWhiteSpace(entry.Executable), $"{key} 缺少 executable");
            Assert.All(entry.ProcessNames ?? [], name => Assert.False(string.IsNullOrWhiteSpace(name)));

            // 原生 exe 必须声明进程名，否则运行状态与「结束进程」都无从判断；
            // 只有批处理启动器（如 Kafka 的 kafka-server-start.bat）才可以留空。
            var isBatchLauncher = entry.Executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                || entry.Executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
            Assert.True(isBatchLauncher || (entry.ProcessNames ?? []).Length > 0,
                $"{key} 声明了 exe 却没有 process_names，运行时无法检测进程状态");
        }
    }

    [Fact]
    public void ProcessesConfig_UsesAppsDirPlaceholder()
    {
        foreach (var (key, entry) in LoadProcesses())
        {
            // 绿色版可迁移：路径必须带 {apps_dir} 占位符，不能写死盘符
            Assert.Contains("{apps_dir}", entry.Executable, StringComparison.Ordinal);
            Assert.Contains("{apps_dir}", entry.WorkingDirectory, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SoftwareConfig_ParsesEveryCategory()
    {
        var config = LoadSoftware();

        Assert.NotEmpty(config);
        foreach (var (category, items) in config)
        {
            Assert.NotNull(items);
            Assert.NotEmpty(items);
            Assert.All(items, item => Assert.False(string.IsNullOrWhiteSpace(item.Name), $"{category} 下有条目缺少 name"));
        }
    }

    [Fact]
    public void SoftwareConfig_EveryVersionIsDownloadable()
    {
        foreach (var (category, items) in LoadSoftware())
        {
            foreach (var item in items)
            {
                Assert.NotEmpty(item.Versions);
                foreach (var version in item.Versions)
                {
                    Assert.False(string.IsNullOrWhiteSpace(version.Version),
                        $"{category}/{item.Name} 的版本号为空（JsonFlexibleStringConverter 没生效？）");
                    Assert.StartsWith("https://", version.Url, StringComparison.OrdinalIgnoreCase);
                    Assert.False(string.IsNullOrWhiteSpace(version.FileName),
                        $"{category}/{item.Name} 缺少 file_name，解压后无法给下载文件命名");
                }
            }
        }
    }

    [Fact]
    public void ConfigFileNames_MatchWhatShipsInResources()
    {
        foreach (var name in new[] { ResourcePaths.ProcessesConfigFile, ResourcePaths.SoftwareCatalogFile, ResourcePaths.DefaultConfigFile })
        {
            Assert.True(File.Exists(ResourcePaths.GetBundledResourcePath(name)), $"Resources/{name} 没有随程序发布");
        }
    }
}