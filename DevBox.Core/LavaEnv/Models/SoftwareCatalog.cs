using System.Text.Json.Serialization;

namespace DevBox.Core.LavaEnv.Models
{
    public class SoftwareCatalogItem
    {
        public string Name { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool Portable { get; set; }
        public List<SoftwareCatalogVersion> Versions { get; set; } = [];
    }

    public class SoftwareCatalogVersion
    {
        [JsonConverter(typeof(JsonFlexibleStringConverter))]
        public string Version { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
