using System.Text.Json;

namespace DevEnv.Core.Components;

/// <summary>组件清单中的一个可下载组件。</summary>
public sealed record ComponentEntry(
    string Id,
    string Name,
    string Version,
    long Size,
    string Sha256,
    IReadOnlyList<string> Urls)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Id)
        && Urls.Count > 0
        && Sha256.Length == 64;
}

/// <summary>
/// <c>components.json</c>：与最新 Release 一起发布的组件清单。
/// 结构：<c>{ "version": 1, "appVersion": "1.3.0", "components": [...] }</c>。
/// </summary>
public sealed class ComponentManifest
{
    public const int SupportedFormatVersion = 1;

    public int FormatVersion { get; init; } = SupportedFormatVersion;
    public string AppVersion { get; init; } = string.Empty;
    public IReadOnlyList<ComponentEntry> Components { get; init; } = [];

    public ComponentEntry? Find(string id) =>
        Components.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    public static ComponentManifest Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("组件清单为空");

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
            throw new InvalidOperationException("无法解析组件清单：不是有效的 JSON", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("组件清单格式错误：根节点必须是对象");

            int format = ReadInt(root, "version", SupportedFormatVersion);
            if (format > SupportedFormatVersion)
                throw new InvalidOperationException($"组件清单版本 {format} 高于当前程序支持的 {SupportedFormatVersion}");

            var entries = new List<ComponentEntry>();
            if (root.TryGetProperty("components", out JsonElement array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in array.EnumerateArray())
                {
                    ComponentEntry? entry = ParseEntry(item);
                    if (entry is not null)
                        entries.Add(entry);
                }
            }

            return new ComponentManifest
            {
                FormatVersion = format,
                AppVersion = ReadString(root, "appVersion"),
                Components = entries,
            };
        }
    }

    private static ComponentEntry? ParseEntry(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        string id = ReadString(item, "id").Trim();
        string name = ReadString(item, "name").Trim();
        string version = ReadString(item, "version").Trim();
        string sha = ReadString(item, "sha256").Trim().ToLowerInvariant();
        long size = ReadLong(item, "size");

        var urls = new List<string>();
        if (item.TryGetProperty("urls", out JsonElement urlArray) && urlArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement url in urlArray.EnumerateArray())
                AddUrl(urls, url.GetString());
        }
        AddUrl(urls, ReadString(item, "url"));

        var entry = new ComponentEntry(
            id,
            string.IsNullOrWhiteSpace(name) ? id : name,
            version,
            size,
            sha,
            urls);

        return entry.IsValid ? entry : null;
    }

    private static void AddUrl(List<string> urls, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri))
            return;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return;
        if (!urls.Contains(uri.ToString(), StringComparer.OrdinalIgnoreCase))
            urls.Add(uri.ToString());
    }

    private static string ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement element, string name, int fallback)
        => element.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : fallback;

    private static long ReadLong(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed) ? parsed : 0;
}
