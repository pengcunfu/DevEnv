using System.Net.Http;
using System.Text.RegularExpressions;

namespace DevBox.Api;

public static class WebMetadataHelper
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        return client;
    }

    public static string? NormalizeUrl(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;
        var trimmed = input.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            trimmed = "https://" + trimmed;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;
        return uri.ToString().TrimEnd('/');
    }

    public static async Task<(string Url, string Title, string IconPath)> FetchAsync(string urlInput)
    {
        var url = NormalizeUrl(urlInput) ?? throw new ArgumentException("无效网址");
        string? html = null;
        try { html = await Http.GetStringAsync(url); }
        catch { /* ignore */ }

        var title = ExtractTitle(html) ?? GetHost(url);
        var faviconUrl = ExtractFavicon(html, url) ?? $"https://www.google.com/s2/favicons?domain={new Uri(url).Host}&sz=128";
        var iconPath = await DownloadIconAsync(faviconUrl, url) ?? "";
        return (url, title, iconPath);
    }

    private static async Task<string?> DownloadIconAsync(string faviconUrl, string pageUrl)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(faviconUrl);
            if (bytes.Length == 0)
                return null;
            return IconHelper.SaveBytesToCache(bytes, $"web_{IconHelper.ComputeHash(pageUrl)}");
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractTitle(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return null;
        var match = Regex.Match(html, @"<title[^>]*>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
            return null;
        var title = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
        return string.IsNullOrWhiteSpace(title) ? null : title;
    }

    private static string? ExtractFavicon(string? html, string pageUrl)
    {
        if (string.IsNullOrEmpty(html))
            return null;
        var baseUri = new Uri(pageUrl);
        const string pattern = @"<link[^>]+rel=[""'](?:shortcut\s+icon|icon|apple-touch-icon)[""'][^>]*>";
        foreach (Match link in Regex.Matches(html, pattern, RegexOptions.IgnoreCase))
        {
            var hrefMatch = Regex.Match(link.Value, @"href=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (!hrefMatch.Success)
                continue;
            if (Uri.TryCreate(baseUri, hrefMatch.Groups[1].Value, out var iconUri))
                return iconUri.ToString();
        }
        return null;
    }

    private static string GetHost(string url)
    {
        try { return new Uri(url).Host; }
        catch { return url; }
    }
}
