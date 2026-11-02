using System.Text.Json;

namespace DevEnv.Core.LavaEnv.Models
{
    /// <summary>
    /// 镜像源清单（mirrors_config.json）的解析规则：顶层按工具名分组，
    /// 每组是一个镜像数组；字段使用 snake_case，允许注释与尾随逗号。
    /// </summary>
    public static class MirrorCatalog
    {
        public static readonly IReadOnlyDictionary<string, List<MirrorSource>> Empty =
            new Dictionary<string, List<MirrorSource>>(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>解析清单；无效条目（缺 key / name / url）与重复 key 会被丢弃。</summary>
        public static Dictionary<string, List<MirrorSource>> Parse(string json)
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, List<MirrorSource>?>>(json, JsonOptions);
            var catalog = new Dictionary<string, List<MirrorSource>>(StringComparer.OrdinalIgnoreCase);
            if (raw is null) return catalog;

            foreach (var (tool, mirrors) in raw)
            {
                var toolName = tool?.Trim();
                if (string.IsNullOrEmpty(toolName) || mirrors is null) continue;

                var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cleaned = new List<MirrorSource>(mirrors.Count);
                foreach (var mirror in mirrors)
                {
                    var normalized = Normalize(mirror);
                    if (normalized is null || !seenKeys.Add(normalized.Key)) continue;
                    cleaned.Add(normalized);
                }

                if (cleaned.Count > 0)
                    catalog[toolName] = cleaned;
            }

            return catalog;
        }

        private static MirrorSource? Normalize(MirrorSource? mirror)
        {
            if (mirror is null) return null;

            var key = mirror.Key?.Trim();
            var name = mirror.Name?.Trim();
            var url = mirror.Url?.Trim();
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
                return null;

            return new MirrorSource
            {
                Key = key,
                Name = name,
                Url = url,
                TrustedHost = string.IsNullOrWhiteSpace(mirror.TrustedHost) ? null : mirror.TrustedHost.Trim(),
                TestUrl = string.IsNullOrWhiteSpace(mirror.TestUrl) ? null : mirror.TestUrl.Trim(),
                Official = mirror.Official || string.Equals(key, "official", StringComparison.OrdinalIgnoreCase)
            };
        }
    }
}
