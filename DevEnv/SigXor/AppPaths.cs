using System.IO;

namespace DevEnv.Host.SigXor;

/// <summary>应用数据目录：用户文档下的提供商目录 / 程序目录（Documents/FNSoftware/SigXor）。</summary>
public static class AppPaths
{
    public const string VendorName = "FNSoftware";
    public const string ProductName = "SigXor";

    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        VendorName,
        ProductName);

    /// <summary>旧版数据目录（Documents/See.Net），启动时若新目录尚无设置则迁入。</summary>
    private static readonly string LegacyDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "See.Net");

    public static string SettingsPath { get; } = Path.Combine(DataDirectory, "settings.json");
    public static string BackupDirectory { get; } = Path.Combine(DataDirectory, "Backups");
    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "Logs");
    /// <summary>临时预览缓存（如 PPT 导出的幻灯片 PNG）。</summary>
    public static string PreviewCacheDirectory { get; } = Path.Combine(DataDirectory, "PreviewCache");
    /// <summary>用户固定的快捷应用列表。</summary>
    public static string PinnedAppsPath { get; } = Path.Combine(DataDirectory, "pinned-apps.json");
    public static string ModelsDirectory { get; } = Path.Combine(DataDirectory, "Models");
    public static string SpeechModelsDirectory { get; } = Path.Combine(ModelsDirectory, "Speech");
    public static string OcrModelsDirectory { get; } = Path.Combine(ModelsDirectory, "Ocr");
    public static string ScreenshotsDirectory { get; } = Path.Combine(DataDirectory, "Screenshots");
    public static string AudioDirectory { get; } = Path.Combine(DataDirectory, "Audio");

    /// <summary>旧版 SigXor 数据目录。产品更名为 SigXor 后它与 <see cref="DataDirectory"/> 同目录，
    /// 用于只读复用旧版布局里的 config.json 与 models（models 目录为小写，Windows 下与 Models 同目录）。</summary>
    public static string LegacySigXorDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        VendorName,
        "SigXor");

    private static readonly object Sync = new();
    private static bool _legacyChecked;

    public static void EnsureCreated()
    {
        lock (Sync)
        {
            TryMigrateFromLegacy();
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(BackupDirectory);
            Directory.CreateDirectory(LogDirectory);
            Directory.CreateDirectory(PreviewCacheDirectory);
            Directory.CreateDirectory(ModelsDirectory);
            Directory.CreateDirectory(SpeechModelsDirectory);
            Directory.CreateDirectory(OcrModelsDirectory);
            Directory.CreateDirectory(ScreenshotsDirectory);
            Directory.CreateDirectory(AudioDirectory);
        }
    }

    private static void TryMigrateFromLegacy()
    {
        if (_legacyChecked) return;
        _legacyChecked = true;
        if (!Directory.Exists(LegacyDataDirectory)) return;

        Directory.CreateDirectory(DataDirectory);
        foreach (string entry in Directory.GetFileSystemEntries(LegacyDataDirectory))
        {
            string dest = Path.Combine(DataDirectory, Path.GetFileName(entry));
            try
            {
                if (Directory.Exists(entry))
                {
                    if (!Directory.Exists(dest)) Directory.Move(entry, dest);
                }
                else if (File.Exists(entry) && !File.Exists(dest))
                {
                    File.Move(entry, dest);
                }
            }
            catch { /* 保留旧文件，下次启动可再试 */ }
        }

        try
        {
            if (Directory.GetFileSystemEntries(LegacyDataDirectory).Length == 0)
                Directory.Delete(LegacyDataDirectory);
        }
        catch { }
    }
}
