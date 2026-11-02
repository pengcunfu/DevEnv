using System.Text.Json.Serialization;

namespace DevEnv.Core.LavaEnv.Models
{
    public class MirrorSource
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? TrustedHost { get; set; }
        /// <summary>测速用的 URL，默认与 Url 相同</summary>
        public string? TestUrl { get; set; }
        /// <summary>是否为官方源；官方源需要恢复工具默认配置，而不是写入镜像地址</summary>
        public bool Official { get; set; }

        [JsonIgnore]
        public double? ResponseTimeMs { get; set; }

        [JsonIgnore]
        public string SpeedTestUrl => string.IsNullOrWhiteSpace(TestUrl) ? Url : TestUrl;

        [JsonIgnore]
        public string DisplayName => ResponseTimeMs.HasValue
            ? $"{Name} - {ResponseTimeMs:0} ms"
            : Name;

        /// <summary>兼容旧配置：key 为 official 的条目一律按官方源处理。</summary>
        [JsonIgnore]
        public bool IsOfficial => Official || string.Equals(Key, "official", StringComparison.OrdinalIgnoreCase);
    }
}
