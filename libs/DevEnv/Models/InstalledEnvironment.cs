namespace DevEnv.Models
{
    public class InstalledEnvironment
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsInPath { get; set; }
    }
}
