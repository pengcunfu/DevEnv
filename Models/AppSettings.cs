namespace DevEnv.Models
{
    public class AppSettings
    {
        public string CacheDir { get; set; } = AppPaths.CacheDir;
        public string AppsDir { get; set; } = AppPaths.AppsDir;
        public bool AutoExtractPortable { get; set; } = true;
        public int MaxWorkers { get; set; } = 4;
        public int ChunkSizeMb { get; set; } = 1;
        public int Timeout { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
        public bool UseSystemProxy { get; set; } = true;
        public string CustomProxy { get; set; } = string.Empty;
        public bool CheckUpdateOnStartup { get; set; }
        public bool MinimizeToTray { get; set; }
    }
}
