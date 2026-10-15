namespace DevEnv.Models
{
    public class MirrorSource
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? TrustedHost { get; set; }
        /// <summary>测速用的 URL，默认与 Url 相同</summary>
        public string? TestUrl { get; set; }
        public double? ResponseTimeMs { get; set; }

        public string SpeedTestUrl => string.IsNullOrWhiteSpace(TestUrl) ? Url : TestUrl;

        public string DisplayName => ResponseTimeMs.HasValue
            ? $"{Name} - {ResponseTimeMs:0} ms"
            : Name;
    }
}
