using System.Xml.Linq;
using DevEnv.Core.LavaEnv.Models;
using DevEnv.Core.LavaEnv.Services;

namespace DevEnv.Tests;

public sealed class MirrorConfigServiceTests
{
    private const string TemplateJson = """
        {
          "npm": [
            { "key": "template", "name": "模板源", "url": "https://template.example.com/" }
          ]
        }
        """;

    [Fact]
    public void Constructor_CopiesTemplateToRuntimePath()
    {
        using var dir = new TempDirectory();
        var template = WriteFile(dir, "template.json", TemplateJson);
        var runtime = Path.Combine(dir.Path, "data", "mirrors_config.json");

        var service = new MirrorConfigService(runtime, template);

        Assert.True(File.Exists(runtime));
        Assert.Equal(runtime, service.CatalogPath);
        var mirror = Assert.Single(service.GetMirrors(MirrorToolType.Npm));
        Assert.Equal("template", mirror.Key);
    }

    [Fact]
    public void RuntimeFile_TakesPrecedenceOverTemplate()
    {
        using var dir = new TempDirectory();
        var template = WriteFile(dir, "template.json", TemplateJson);
        var runtime = WriteFile(dir, "mirrors_config.json", """
            {
              "npm": [
                { "key": "custom", "name": "自定义源", "url": "https://custom.example.com/" }
              ]
            }
            """);

        var service = new MirrorConfigService(runtime, template);

        var mirror = Assert.Single(service.GetMirrors(MirrorToolType.Npm));
        Assert.Equal("custom", mirror.Key);
        Assert.Equal("https://custom.example.com/", mirror.Url);
    }

    [Fact]
    public void BrokenRuntimeFile_FallsBackToTemplate()
    {
        using var dir = new TempDirectory();
        var template = WriteFile(dir, "template.json", TemplateJson);
        var runtime = WriteFile(dir, "mirrors_config.json", """{ "npm": [ 这里不是合法 JSON """);

        var service = new MirrorConfigService(runtime, template);

        var mirror = Assert.Single(service.GetMirrors(MirrorToolType.Npm));
        Assert.Equal("template", mirror.Key);
    }

    [Fact]
    public void EmptyRuntimeFile_MeansUserClearedCatalog_NotFallback()
    {
        using var dir = new TempDirectory();
        var template = WriteFile(dir, "template.json", TemplateJson);
        var runtime = WriteFile(dir, "mirrors_config.json", "{}");

        var service = new MirrorConfigService(runtime, template);

        Assert.Empty(service.GetMirrors(MirrorToolType.Npm));
    }

    [Fact]
    public void MissingFiles_ReturnEmptyCatalog()
    {
        using var dir = new TempDirectory();
        var service = new MirrorConfigService(Path.Combine(dir.Path, "missing.json"));

        Assert.Empty(service.GetMirrors(MirrorToolType.Pip));
        Assert.Empty(service.LoadCatalog());
    }

    [Fact]
    public void UnknownTool_ReturnsEmptyList()
    {
        using var dir = new TempDirectory();
        var runtime = WriteFile(dir, "mirrors_config.json", TemplateJson);

        var service = new MirrorConfigService(runtime);

        Assert.Empty(service.GetMirrors(MirrorToolType.Pip));
    }

    [Fact]
    public void ApplyMavenMirror_CreatesSettingsFile()
    {
        var mirror = new MirrorSource
        {
            Key = "aliyun",
            Name = "阿里云公共仓库",
            Url = "https://maven.aliyun.com/repository/public",
        };

        var content = MirrorConfigService.ApplyMavenMirror(null, [mirror.Url], mirror);

        XNamespace ns = "http://maven.apache.org/SETTINGS/1.0.0";
        var doc = XDocument.Parse(content);
        var entry = Assert.Single(doc.Descendants(ns + "mirror"));
        Assert.Equal("lava", entry.Element(ns + "id")?.Value);
        Assert.Equal("*", entry.Element(ns + "mirrorOf")?.Value);
        Assert.Equal(mirror.Url, entry.Element(ns + "url")?.Value);
        Assert.NotNull(doc.Root?.Attribute(XNamespace.Xmlns + "xsi"));
    }

    [Fact]
    public void ApplyMavenMirror_PreservesUserSettingsAndReplacesOldManagedEntries()
    {
        const string existing = """
            <?xml version="1.0" encoding="UTF-8"?>
            <settings xmlns="http://maven.apache.org/SETTINGS/1.0.0">
              <servers>
                <server><id>nexus</id><username>dev</username></server>
              </servers>
              <mirrors>
                <mirror>
                  <id>maven-aliyun-com</id>
                  <mirrorOf>*</mirrorOf>
                  <name>旧版阿里云</name>
                  <url>https://maven.aliyun.com/repository/public</url>
                </mirror>
                <mirror>
                  <id>company</id>
                  <mirrorOf>central</mirrorOf>
                  <name>公司私服</name>
                  <url>https://nexus.company.local/repository/maven-public/</url>
                </mirror>
              </mirrors>
            </settings>
            """;
        var mirror = new MirrorSource
        {
            Key = "tencent",
            Name = "腾讯云镜像",
            Url = "https://mirrors.cloud.tencent.com/nexus/repository/maven-public/",
        };

        var content = MirrorConfigService.ApplyMavenMirror(
            existing,
            ["https://maven.aliyun.com/repository/public", mirror.Url],
            mirror);

        XNamespace ns = "http://maven.apache.org/SETTINGS/1.0.0";
        var doc = XDocument.Parse(content);
        Assert.Single(doc.Descendants(ns + "server"));
        Assert.Equal("dev", doc.Descendants(ns + "username").Single().Value);

        var mirrors = doc.Descendants(ns + "mirror").ToList();
        Assert.Equal(2, mirrors.Count);
        Assert.DoesNotContain(mirrors, m => (string?)m.Element(ns + "id") == "maven-aliyun-com");
        Assert.Contains(mirrors, m => (string?)m.Element(ns + "id") == "company");
        var managed = Assert.Single(mirrors, m => (string?)m.Element(ns + "id") == "lava");
        Assert.Equal(mirror.Url, managed.Element(ns + "url")?.Value);
    }

    [Fact]
    public void ApplyMavenMirror_SupportsSettingsWithoutNamespace()
    {
        const string existing = """
            <settings>
              <mirrors>
                <mirror><id>lava</id><mirrorOf>*</mirrorOf><name>旧</name><url>https://old.example.com/</url></mirror>
              </mirrors>
            </settings>
            """;
        var mirror = new MirrorSource { Key = "official", Name = "Maven 中央仓库", Url = "https://repo1.maven.org/maven2/" };

        var content = MirrorConfigService.ApplyMavenMirror(existing, [], mirror);

        XNamespace ns = XNamespace.None;
        var entry = Assert.Single(XDocument.Parse(content).Descendants(ns + "mirror"));
        Assert.Equal(mirror.Url, entry.Element(ns + "url")?.Value);
    }

    private static string WriteFile(TempDirectory dir, string name, string content)
    {
        var path = Path.Combine(dir.Path, name);
        File.WriteAllText(path, content);
        return path;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "DevEnvTests", Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch
            {
                // 测试清理失败不影响断言
            }
        }
    }
}
