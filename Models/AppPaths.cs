using System.IO;

namespace DevEnv.Models
{
  public static class AppPaths
  {
    public const string Root = @"D:\devenv";

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
