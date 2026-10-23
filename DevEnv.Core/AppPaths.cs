namespace DevEnv.Core;

/// <summary>用户数据路径：文档\FNSoftware\DevEnv（与熔岩模块共用根目录）。</summary>
public static class AppPaths
{
    public const string VendorName = "FNSoftware";
    public const string AppDirectoryName = "DevEnv";
    private const string LegacyAppDirectoryName = "DevBox";

    public static string UserDataDirectory { get; } = ResolveUserDataDirectory();
    public static string DataJsonPath => Path.Combine(UserDataDirectory, "data.json");

    private static string ResolveUserDataDirectory()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var directory = Path.Combine(documents, VendorName, AppDirectoryName);
        Directory.CreateDirectory(directory);
        MigrateLegacyDevBoxData(documents, directory);
        return directory;
    }

    /// <summary>
    /// 从旧版「文档\FNSoftware\DevBox」迁入主程序文件；不覆盖已有项，也不动熔岩的 config.json。
    /// </summary>
    private static void MigrateLegacyDevBoxData(string documents, string dest)
    {
        var legacy = Path.Combine(documents, VendorName, LegacyAppDirectoryName);
        if (!Directory.Exists(legacy))
            return;

        TryMigrateFile(Path.Combine(legacy, "data.json"), Path.Combine(dest, "data.json"));

        var legacyIcons = Path.Combine(legacy, "icons");
        var destIcons = Path.Combine(dest, "icons");
        if (Directory.Exists(legacyIcons) && !Directory.Exists(destIcons))
        {
            try
            {
                Directory.Move(legacyIcons, destIcons);
            }
            catch
            {
                // ignore — 下次启动再试
            }
        }
    }

    private static void TryMigrateFile(string src, string dst)
    {
        if (!File.Exists(src) || File.Exists(dst))
            return;
        try
        {
            File.Move(src, dst);
        }
        catch
        {
            // ignore
        }
    }
}
