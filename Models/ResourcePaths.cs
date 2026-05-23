using System.IO;

namespace DevEnv.Models
{
    /// <summary>内置资源目录名，源码位于 Resources/，构建时复制到输出目录。</summary>
    public static class ResourcePaths
    {
        public const string ResourcesFolderName = "Resources";
        public const string SoftwareCatalogFile = "software_config.json";
        public const string ProcessesConfigFile = "processes_config.json";
        /// <summary>默认配置模板，首次运行时复制到 D:\devenv\config.json</summary>
        public const string DefaultConfigFile = "config.json";
        public const string Bundled7zrExe = "tools/7zr.exe";

        public static string ResourcesDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ResourcesFolderName);

        public static string GetBundledResourcePath(string fileName) =>
            Path.Combine(ResourcesDirectory, fileName);
    }
}
