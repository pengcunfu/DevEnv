using System.Collections.Generic;

namespace DevEnv.Models
{
    public class SoftwareVersion
    {
        public string Version { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class SoftwareInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<SoftwareVersion> Versions { get; set; } = new List<SoftwareVersion>();
    }
}
