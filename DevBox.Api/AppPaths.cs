namespace DevBox.Api;

/// <summary>与现版 DevBox 一致的用户数据路径（文档\FNSoftware\DevBox）。</summary>
public static class AppPaths
{
    public const string VendorName = "FNSoftware";
    public const string AppDirectoryName = "DevBox";

    public static string UserDataDirectory { get; } = ResolveUserDataDirectory();
    public static string DataJsonPath => Path.Combine(UserDataDirectory, "data.json");
    public static string ConfigJsonPath => Path.Combine(UserDataDirectory, "config.json");

    private static string ResolveUserDataDirectory()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var directory = Path.Combine(documents, VendorName, AppDirectoryName);
        Directory.CreateDirectory(directory);
        return directory;
    }
}
