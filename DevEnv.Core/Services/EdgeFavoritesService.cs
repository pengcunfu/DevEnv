using System.Text;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace DevEnv.Core.Services;

/// <summary>Edge 收藏夹里的一条网址收藏。</summary>
public sealed record EdgeBookmark(string Name, string Url, string ProfileDirectory);

/// <summary>解析 Edge 的 Bookmarks JSON（Default / Profile N 结构相同）。</summary>
public static class EdgeBookmarkParser
{
    public static IReadOnlyList<EdgeBookmark> Parse(string? json, string profileDirectory)
    {
        var bookmarks = new List<EdgeBookmark>();
        if (string.IsNullOrWhiteSpace(json))
            return bookmarks;

        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch
        {
            return bookmarks;
        }

        if (root["roots"] is JObject roots)
        {
            foreach (var rootNode in roots.Properties())
                Walk(rootNode.Value, profileDirectory, bookmarks);
        }

        return bookmarks;
    }

    private static void Walk(JToken? node, string profileDirectory, List<EdgeBookmark> bookmarks)
    {
        if (node is not JObject obj)
            return;

        if (string.Equals(obj["type"]?.ToString(), "url", StringComparison.OrdinalIgnoreCase))
        {
            var url = obj["url"]?.ToString();
            if (IsSupportedUrl(url))
            {
                var name = obj["name"]?.ToString();
                bookmarks.Add(new EdgeBookmark(
                    string.IsNullOrWhiteSpace(name) ? url! : name,
                    url!,
                    profileDirectory));
            }

            return;
        }

        if (obj["children"] is JArray children)
        {
            foreach (var child in children)
                Walk(child, profileDirectory, bookmarks);
        }
    }

    private static bool IsSupportedUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

/// <summary>启动器网址与 Edge 收藏的匹配（先精确网址，再同源，最后同主机）。</summary>
public static class EdgeFavoriteMatcher
{
    public static EdgeBookmark? FindBestMatch(IEnumerable<EdgeBookmark> bookmarks, string? url)
    {
        var key = StartMenuIndex.NormalizeUrlForCompare(url);
        if (key.Length == 0)
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        EdgeBookmark? originMatch = null;
        EdgeBookmark? hostMatch = null;

        foreach (var bookmark in bookmarks)
        {
            var bookmarkKey = StartMenuIndex.NormalizeUrlForCompare(bookmark.Url);
            if (string.Equals(bookmarkKey, key, StringComparison.OrdinalIgnoreCase))
                return bookmark;

            if (!Uri.TryCreate(bookmark.Url, UriKind.Absolute, out var bookmarkUri)
                || !string.Equals(bookmarkUri.Host, uri.Host, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(bookmarkUri.Scheme, uri.Scheme, StringComparison.OrdinalIgnoreCase)
                && bookmarkUri.Port == uri.Port)
            {
                if (originMatch is null || bookmark.Url.Length < originMatch.Url.Length)
                    originMatch = bookmark;
            }
            else if (hostMatch is null || bookmark.Url.Length < hostMatch.Url.Length)
            {
                hostMatch = bookmark;
            }
        }

        return originMatch ?? hostMatch;
    }
}

/// <summary>
/// 读取 Edge 收藏夹并用收藏对应页面在 Edge 里缓存的 favicon 作为启动器网站图标。
/// 只对 Edge 收藏里出现过的站点生效，其它网站仍使用原来抓取的 favicon；
/// Edge 未安装、数据库被占用或读取失败时静默降级，不影响启动器。
/// </summary>
public sealed class EdgeFavoritesService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();
    private readonly List<EdgeBookmark> _bookmarks = [];
    private readonly List<string> _profiles = [];
    private readonly Dictionary<string, string?> _icons = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _loadedAtUtc = DateTime.MinValue;

    /// <summary>
    /// 取网址在 Edge 里缓存的站点图标：优先用收藏夹里对应页面的 favicon，
    /// 没有收藏记录时复用 Edge 访问过的同站图标。找不到时返回 null。
    /// <paramref name="force"/> 为 true 时重新读取收藏与图标缓存（右键「刷新图标」）。
    /// </summary>
    public string? TryGetFavoriteIcon(string? url, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var key = StartMenuIndex.NormalizeUrlForCompare(url);
        if (key.Length == 0)
            return null;

        lock (_gate)
        {
            EnsureLoadedLocked(force);

            if (force)
                _icons.Remove(key);

            if (_icons.TryGetValue(key, out var cached))
                return cached;

            var match = EdgeFavoriteMatcher.FindBestMatch(_bookmarks, url);
            string? icon = null;
            if (match is not null)
            {
                icon = QueryIcon(match.ProfileDirectory, match.Url)
                    ?? QueryIcon(match.ProfileDirectory, url);
            }

            // 不是收藏站点或收藏里没有图标时，退回 Edge 已缓存的同站图标
            icon ??= QueryAnyProfile(url);

            _icons[key] = icon;
            return icon;
        }
    }

    private void EnsureLoadedLocked(bool force)
    {
        if (!force
            && _loadedAtUtc != DateTime.MinValue
            && DateTime.UtcNow - _loadedAtUtc < CacheLifetime)
        {
            return;
        }

        _bookmarks.Clear();
        _profiles.Clear();
        _icons.Clear();

        foreach (var root in GetUserDataRoots())
        {
            if (!Directory.Exists(root))
                continue;

            try
            {
                foreach (var profile in Directory.EnumerateDirectories(root))
                {
                    var bookmarksFile = Path.Combine(profile, "Bookmarks");
                    if (!File.Exists(bookmarksFile))
                        continue;

                    _profiles.Add(profile);
                    try
                    {
                        _bookmarks.AddRange(
                            EdgeBookmarkParser.Parse(ReadAllTextShared(bookmarksFile), profile));
                    }
                    catch
                    {
                        // 单个配置文件读失败不影响其它配置
                    }
                }
            }
            catch
            {
                // 目录不可读时跳过
            }
        }

        _loadedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Edge 稳定版以及 Beta / Dev / Canary 的用户数据目录。</summary>
    private static IEnumerable<string> GetUserDataRoots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            yield break;

        foreach (var name in new[] { "Edge", "Edge Beta", "Edge Dev", "Edge SxS" })
            yield return Path.Combine(local, "Microsoft", name, "User Data");
    }

    private static string ReadAllTextShared(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private string? QueryAnyProfile(string pageUrl)
    {
        foreach (var profile in _profiles)
        {
            var icon = QueryIcon(profile, pageUrl);
            if (!string.IsNullOrWhiteSpace(icon))
                return icon;
        }

        return null;
    }

    private static string? QueryIcon(string profileDirectory, string pageUrl)
    {
        var database = Path.Combine(profileDirectory, "Favicons");
        if (!File.Exists(database))
            return null;

        try
        {
            using var connection = new SqliteConnection($"Data Source={database};Mode=ReadOnly");
            connection.Open();

            var bytes = QueryBitmap(connection, pageUrl, exact: true);
            if (bytes is not null)
                return SaveIcon(bytes, pageUrl);

            if (Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri))
            {
                var origin = uri.GetLeftPart(UriPartial.Authority) + "/";

                bytes = QueryBitmap(connection, origin, exact: true)
                    ?? QueryBitmap(connection, origin + "%", exact: false);
                if (bytes is not null)
                    return SaveIcon(bytes, pageUrl);
            }

            return null;
        }
        catch
        {
            // Edge 正在运行、数据库被占用或 schema 变化时保持原图标
            return null;
        }
    }

    private static byte[]? QueryBitmap(SqliteConnection connection, string pattern, bool exact)
    {
        using var command = connection.CreateCommand();
        command.CommandText = exact
            ? """
              select b.image_data
              from icon_mapping m join favicon_bitmaps b on b.icon_id = m.icon_id
              where lower(m.page_url) = lower($p)
              order by b.width desc, b.height desc, b.last_updated desc
              limit 1
              """
            : """
              select b.image_data
              from icon_mapping m join favicon_bitmaps b on b.icon_id = m.icon_id
              where lower(m.page_url) like lower($p)
              order by b.width desc, b.height desc, b.last_updated desc
              limit 1
              """;
        command.Parameters.AddWithValue("$p", pattern);

        using var reader = command.ExecuteReader();
        return reader.Read() && !reader.IsDBNull(0) ? (byte[])reader.GetValue(0) : null;
    }

    private static string? SaveIcon(byte[] bytes, string pageUrl)
    {
        if (bytes.Length == 0)
            return null;

        // 文件名带上内容指纹：Edge 换了图标就会得到新路径，界面不用依赖额外的失效通知
        var contentKey = IconHelper.ComputeHash(Convert.ToHexString(bytes))[..8];
        return IconHelper.SaveBytesToCache(
            bytes,
            $"edge_{IconHelper.ComputeHash(pageUrl)}_{contentKey}",
            ".png");
    }
}
