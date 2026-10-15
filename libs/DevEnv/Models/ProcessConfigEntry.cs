namespace DevEnv.Models
{
    public class ProcessConfigEntry
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] ProcessNames { get; set; } = [];
        public string Executable { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
    }
}
