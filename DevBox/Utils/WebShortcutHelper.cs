using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ProgramBox.Models;

namespace ProgramBox.Utils
{
    public static class WebShortcutHelper
    {
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
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
            {
                trimmed = "https://" + trimmed;
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return null;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return null;

            return uri.ToString().TrimEnd('/');
        }

        public static async Task<WebMetadata> FetchMetadataAsync(string urlInput)
        {
            var url = NormalizeUrl(urlInput);
            if (url == null)
                throw new ArgumentException("请输入有效的网址，例如 https://github.com");

            string? html = null;
            try
            {
                html = await Http.GetStringAsync(url);
            }
            catch
            {
                // 部分站点禁止抓取 HTML，仍尝试 favicon
            }

            var title = ExtractTitle(html) ?? GetHostLabel(url);
            var faviconUrl = ExtractFaviconUrl(html, url) ?? GetGoogleFaviconUrl(url);
            var iconPath = await DownloadIconToCacheAsync(faviconUrl, url);

            return new WebMetadata(url, title, iconPath ?? string.Empty);
        }

        public static async Task<WebAtom?> CreateFromUrlAsync(string urlInput)
        {
            var meta = await FetchMetadataAsync(urlInput);
            return new WebAtom(meta.Title, meta.IconPath, "web", meta.Url, meta.Url);
        }

        public static async Task<string?> RefreshFaviconAsync(WebAtom web)
        {
            var faviconUrl = GetGoogleFaviconUrl(web.Url);
            try
            {
                if (!string.IsNullOrEmpty(web.Url))
                {
                    var html = await Http.GetStringAsync(web.Url);
                    faviconUrl = ExtractFaviconUrl(html, web.Url) ?? faviconUrl;
                }
            }
            catch
            {
                // use google fallback
            }

            IconCacheHelper.TryDeleteCachedIcon(web.IconPath);
            return await DownloadIconToCacheAsync(faviconUrl, web.Url);
        }

        public static string? SetCustomIcon(WebAtom web, string imageFilePath)
        {
            if (!File.Exists(imageFilePath))
                return null;

            var cacheKey = $"web_{IconCacheHelper.ComputeHash(web.Url)}_custom";
            IconCacheHelper.TryDeleteCachedIcon(web.IconPath);
            return IconCacheHelper.CopyImageToCache(imageFilePath, cacheKey);
        }

        public static bool IsSameUrl(string urlA, string urlB)
        {
            var a = NormalizeUrl(urlA);
            var b = NormalizeUrl(urlB);
            if (a == null || b == null)
                return false;

            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<string?> DownloadIconToCacheAsync(string faviconUrl, string pageUrl)
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync(faviconUrl);
                if (bytes.Length == 0)
                    return null;

                Directory.CreateDirectory(IconCacheHelper.IconsDirectory);
                var cacheKey = $"web_{IconCacheHelper.ComputeHash(pageUrl)}";
                var destPath = Path.Combine(IconCacheHelper.IconsDirectory, $"{cacheKey}.png");

                await File.WriteAllBytesAsync(destPath, bytes);
                return destPath;
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

            var match = Regex.Match(html, @"<title[^>]*>\s*(.*?)\s*</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                return null;

            var title = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }

        private static string? ExtractFaviconUrl(string? html, string pageUrl)
        {
            if (string.IsNullOrEmpty(html))
                return null;

            var baseUri = new Uri(pageUrl);
            const string pattern = @"<link[^>]+rel=[""'](?:shortcut\s+icon|icon|apple-touch-icon)[""'][^>]*>";

            foreach (Match link in Regex.Matches(html, pattern, RegexOptions.IgnoreCase))
            {
                var tag = link.Value;
                var hrefMatch = Regex.Match(tag, @"href=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                if (!hrefMatch.Success)
                    continue;

                var href = hrefMatch.Groups[1].Value;
                if (Uri.TryCreate(baseUri, href, out var iconUri))
                    return iconUri.ToString();
            }

            return null;
        }

        private static string GetGoogleFaviconUrl(string pageUrl)
        {
            var uri = new Uri(pageUrl);
            return $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=128";
        }

        private static string GetHostLabel(string url)
        {
            try
            {
                return new Uri(url).Host;
            }
            catch
            {
                return url;
            }
        }
    }

    public sealed record WebMetadata(string Url, string Title, string IconPath);
}
