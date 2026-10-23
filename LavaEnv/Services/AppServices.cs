namespace DevEnv.Services
{
    public static class AppServices
    {
        public static AppConfigService Config { get; } = new();
        public static DownloadHistoryService DownloadHistory { get; } = new();
        public static PortableInstallService PortableInstall { get; } = new(Config);
        public static DownloadService Download { get; } = new(Config, DownloadHistory, PortableInstall);
        public static ProcessManager ProcessManager { get; } = new(Config);
        public static MirrorConfigService MirrorConfig { get; } = new();
        public static EnvironmentScannerService EnvironmentScanner { get; } = new();
    }
}
