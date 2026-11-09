using System.Text.Json;

namespace DevEnv.Core.Updates;

/// <summary><c>latest.json</c> 中的一次可用更新。</summary>
public sealed record UpdateManifest(string Version, string Url, string Sha256, long Size)
{
    public bool IsNewerThan(Version current) =>
        AppVersions.TryParse(Version, out Version candidate) && AppVersions.IsNewer(candidate, current);
}

/// <summary>解析 Release 里的 <c>latest.json</c>。</summary>
public static class UpdateManifestParser
{
    public static UpdateManifest Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("更新清单为空");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("无法解析更新清单：不是有效的 JSON", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("更新清单格式错误：根节点必须是对象");

            string version = ReadString(root, "version").Trim();
            if (version.Length == 0)
                throw new InvalidOperationException("更新清单缺少版本号");
            if (!AppVersions.TryParse(version, out _))
                throw new InvalidOperationException($"版本号无法比较：{version}");

            string url = ReadString(root, "url").Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                throw new InvalidOperationException("更新清单缺少下载地址");
            }

            string sha = ReadString(root, "sha256").Trim().ToLowerInvariant();
            if (sha.Length != 64 || !sha.All(IsHex))
                throw new InvalidOperationException("更新清单缺少有效的 SHA256");

            return new UpdateManifest(version, uri.ToString(), sha, ReadLong(root, "size"));
        }
    }

    private static bool IsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');

    private static string ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static long ReadLong(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed) ? parsed : 0;
}
