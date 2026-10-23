using DevEnv.Core.Translator.Models;

namespace DevEnv.Tests;

public sealed class WebTranslateCatalogTests
{
    [Fact]
    public void All_IdsAreUnique()
    {
        var ids = WebTranslateCatalog.All.Select(s => s.Id).ToArray();

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void All_HomeUrlsAreAbsoluteHttps()
    {
        foreach (var site in WebTranslateCatalog.All)
        {
            Assert.True(site.HomeUrl.IsAbsoluteUri, $"{site.Id} 的首页地址不是绝对路径");
            Assert.Equal(Uri.UriSchemeHttps, site.HomeUrl.Scheme);
        }
    }

    [Fact]
    public void FindById_IsCaseInsensitive()
    {
        var first = WebTranslateCatalog.All[0];

        Assert.NotNull(WebTranslateCatalog.FindById(first.Id.ToUpperInvariant()));
        Assert.Equal(first.Id, WebTranslateCatalog.FindById(first.Id)!.Id);
    }

    [Fact]
    public void FindById_UnknownOrNull_ReturnsNull()
    {
        Assert.Null(WebTranslateCatalog.FindById("no-such-site"));
        Assert.Null(WebTranslateCatalog.FindById(null));
    }

    [Fact]
    public void ResolveUrl_WithoutTextOrBuilder_ReturnsHomeUrl()
    {
        var site = WebTranslateCatalog.All[0];

        Assert.Equal(site.HomeUrl, site.ResolveUrl(null));
        Assert.Equal(site.HomeUrl, site.ResolveUrl("   "));
    }

    [Fact]
    public void ResolveUrl_WithBuilder_TrimsTextAndBuildsUrl()
    {
        var site = new WebTranslateSite
        {
            Id = "sample",
            Name = "示例",
            HomeUrl = new Uri("https://example.com/"),
            BuildUrlWithText = text => new Uri($"https://example.com/?text={Uri.EscapeDataString(text)}"),
        };

        Assert.Equal("https://example.com/?text=hello%20world", site.ResolveUrl("  hello world  ").AbsoluteUri);
    }
}