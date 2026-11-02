using DevEnv.Core.LavaEnv.Models;

namespace DevEnv.Tests;

public sealed class MirrorCatalogTests
{
    [Fact]
    public void Parse_ReadsSnakeCaseFields()
    {
        var catalog = MirrorCatalog.Parse("""
            {
              "pip": [
                {
                  "key": "tsinghua",
                  "name": "清华大学",
                  "url": "https://pypi.tuna.tsinghua.edu.cn/simple",
                  "trusted_host": "pypi.tuna.tsinghua.edu.cn"
                },
                {
                  "key": "official",
                  "name": "官方源",
                  "url": "https://pypi.org/simple",
                  "official": true
                }
              ]
            }
            """);

        var mirrors = catalog["pip"];

        Assert.Equal(2, mirrors.Count);
        Assert.Equal("pypi.tuna.tsinghua.edu.cn", mirrors[0].TrustedHost);
        Assert.False(mirrors[0].IsOfficial);
        Assert.True(mirrors[1].IsOfficial);
    }

    [Fact]
    public void Parse_AllowsCommentsAndTrailingCommas()
    {
        var catalog = MirrorCatalog.Parse("""
            {
              // 用户手改时常常顺手写注释
              "npm": [
                { "key": "npmmirror", "name": "淘宝", "url": "https://registry.npmmirror.com/", },
              ],
            }
            """);

        var mirror = Assert.Single(catalog["npm"]);
        Assert.Equal("npmmirror", mirror.Key);
    }

    [Fact]
    public void Parse_DropsInvalidAndDuplicateEntries()
    {
        var catalog = MirrorCatalog.Parse("""
            {
              "pip": [
                null,
                { "key": "no-name", "url": "https://a.example/simple" },
                { "key": "no-url", "name": "缺地址" },
                { "key": "aliyun", "name": "阿里云", "url": "https://mirrors.aliyun.com/pypi/simple/" },
                { "key": "ALIYUN", "name": "阿里云重复项", "url": "https://mirrors.aliyun.com/dup/" },
                { "key": "ustc", "name": "中科大", "url": "https://pypi.mirrors.ustc.edu.cn/simple/" }
              ],
              "empty": [],
              "nullSection": null
            }
            """);

        Assert.Equal(2, catalog["pip"].Count);
        Assert.Equal("aliyun", catalog["pip"][0].Key);
        Assert.Equal("ustc", catalog["pip"][1].Key);
        Assert.False(catalog.ContainsKey("empty"));
        Assert.False(catalog.ContainsKey("nullSection"));
    }

    [Fact]
    public void Parse_ToolNamesAreCaseInsensitive()
    {
        var catalog = MirrorCatalog.Parse("""{ "NuGet": [ { "key": "huawei", "name": "华为云", "url": "https://repo.huaweicloud.com/repository/nuget/v3/index.json" } ] }""");

        Assert.True(catalog.TryGetValue("nuget", out var mirrors));
        Assert.Single(mirrors);
    }

    [Fact]
    public void Parse_NullJson_ReturnsEmptyCatalog()
    {
        Assert.Empty(MirrorCatalog.Parse("null"));
        Assert.Empty(MirrorCatalog.Parse("{}"));
    }

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        var catalog = MirrorCatalog.Parse("""{ " pip " : [ { "key": " tsinghua ", "name": " 清华 ", "url": " https://pypi.tuna.tsinghua.edu.cn/simple " } ] }""");

        var mirror = Assert.Single(catalog["pip"]);
        Assert.Equal("tsinghua", mirror.Key);
        Assert.Equal("清华", mirror.Name);
        Assert.Equal("https://pypi.tuna.tsinghua.edu.cn/simple", mirror.Url);
    }
}
