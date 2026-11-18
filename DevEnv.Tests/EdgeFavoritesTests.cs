using DevEnv.Core.Services;

namespace DevEnv.Tests;

/// <summary>Edge 收藏 JSON 解析与启动器网址匹配规则（不依赖真实 Edge 数据）。</summary>
public sealed class EdgeFavoritesTests
{
    private const string Sample = """
        {
          "roots": {
            "bookmark_bar": {
              "children": [
                { "type": "url", "name": "GitHub", "url": "https://github.com/" },
                {
                  "type": "folder",
                  "name": "开发",
                  "children": [
                    { "type": "url", "name": "Docs", "url": "https://example.com/docs" },
                    { "type": "url", "name": "本地文件", "url": "file:///C:/test.html" }
                  ]
                }
              ]
            },
            "other": {
              "children": [
                { "type": "url", "name": "博客", "url": "https://pengcunfu.com/" }
              ]
            }
          }
        }
        """;

    [Fact]
    public void Parser_FlattensNestedFoldersAndSkipsNonHttpUrls()
    {
        var bookmarks = EdgeBookmarkParser.Parse(Sample, @"C:\Edge\Default");

        Assert.Equal(new[] { "GitHub", "Docs", "博客" }, bookmarks.Select(item => item.Name));
        Assert.Equal(
            new[] { "https://github.com/", "https://example.com/docs", "https://pengcunfu.com/" },
            bookmarks.Select(item => item.Url));
        Assert.All(bookmarks, item => Assert.Equal(@"C:\Edge\Default", item.ProfileDirectory));
    }

    [Fact]
    public void Parser_ReturnsEmptyForInvalidJson()
    {
        Assert.Empty(EdgeBookmarkParser.Parse("not json", @"C:\Edge\Default"));
        Assert.Empty(EdgeBookmarkParser.Parse(null, @"C:\Edge\Default"));
    }

    [Fact]
    public void Matcher_PrefersExactUrl()
    {
        var bookmarks = new[]
        {
            new EdgeBookmark("首页", "https://example.com/", @"C:\Edge\Default"),
            new EdgeBookmark("文档", "https://example.com/docs", @"C:\Edge\Default"),
        };

        var match = EdgeFavoriteMatcher.FindBestMatch(bookmarks, "https://example.com/docs/");

        Assert.NotNull(match);
        Assert.Equal("文档", match.Name);
    }

    [Fact]
    public void Matcher_FallsBackToSameOrigin()
    {
        var bookmarks = new[]
        {
            new EdgeBookmark("仓库", "https://github.com/OpenDocCN/repo", @"C:\Edge\Default"),
            new EdgeBookmark("其它", "https://example.com/", @"C:\Edge\Default"),
        };

        var match = EdgeFavoriteMatcher.FindBestMatch(bookmarks, "https://github.com/");

        Assert.NotNull(match);
        Assert.Equal("仓库", match.Name);
    }

    [Fact]
    public void Matcher_FallsBackToSameHostAcrossSchemes()
    {
        var bookmarks = new[]
        {
            new EdgeBookmark("旧地址", "http://example.com/page", @"C:\Edge\Default"),
        };

        var match = EdgeFavoriteMatcher.FindBestMatch(bookmarks, "https://example.com/");

        Assert.NotNull(match);
        Assert.Equal("旧地址", match.Name);
    }

    [Fact]
    public void Matcher_ReturnsNullWhenSiteIsNotFavorite()
    {
        var bookmarks = new[]
        {
            new EdgeBookmark("GitHub", "https://github.com/", @"C:\Edge\Default"),
        };

        Assert.Null(EdgeFavoriteMatcher.FindBestMatch(bookmarks, "https://pengcunfu.com/"));
    }
}
