using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace DevEnv.Models
{
    public class SoftwareVersion
    {
        public string Version { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public class SoftwareInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public ObservableCollection<SoftwareVersion> Versions { get; set; } = [];
    }
}
