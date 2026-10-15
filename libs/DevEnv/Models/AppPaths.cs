using System;
using System.IO;

namespace DevEnv.Models
{
  public static class AppPaths
  {
    /// <summary>软件提供商目录名（位于用户「文档」下）。</summary>
    public const string VendorFolderName = "FNSoftware";

    /// <summary>本程序数据目录名。</summary>
    public const string AppFolderName = "DevEnv";

    /// <summary>
    /// 用户数据根目录：文档\FNSoftware\DevEnv
    /// （例如 C:\Users\&lt;user&gt;\Documents\FNSoftware\DevEnv）
    /// </summary>
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        VendorFolderName,
        AppFolderName);

    public static string ConfigFile => Path.Combine(Root, "config.json");
    public static string DownloadHistoryFile => Path.Combine(Root, "download_history.json");
    public static string ProcessesStateFile => Path.Combine(Root, "processes_state.json");
    public static string CacheDir => Path.Combine(Root, "cache");
    public static string AppsDir => Path.Combine(Root, "apps");

    public static void EnsureDirectories()
    {
      Directory.CreateDirectory(Root);
      Directory.CreateDirectory(CacheDir);
      Directory.CreateDirectory(AppsDir);
    }
  }
}
