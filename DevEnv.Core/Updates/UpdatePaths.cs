namespace DevEnv.Core.Updates;

/// <summary>自研更新器使用的固定路径与文件名。</summary>
public static class UpdatePaths
{
    public const string BackupSuffix = ".bak";
    public const string MarkerFileName = ".update_ok";
    public const string LogFileName = "updater.log";

    public static string LocalRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevEnv");

    public static string UpdatesRoot => Path.Combine(LocalRoot, "Updates");

    public static string LogPath => Path.Combine(LocalRoot, LogFileName);

    public static string MarkerPath => Path.Combine(AppContext.BaseDirectory, MarkerFileName);

    public static string BackupExePath(string exePath) => exePath + BackupSuffix;

    public static string ResourcesDirectory => Path.Combine(AppContext.BaseDirectory, "Resources");

    public static string ResourcesBackupDirectory => ResourcesDirectory + BackupSuffix;
}
