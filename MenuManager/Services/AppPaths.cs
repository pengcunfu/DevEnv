using System.IO;

namespace MenuManager.Services
{
    /// <summary>
    /// 应用程序路径
    /// </summary>
    public static class AppPaths
    {
        public const string VendorName = "FNSoftware";
        public const string AppName = "MenuManager";
        public const string ConfigFileName = "menu_configs.json";

        /// <summary>
        /// 用户数据目录：文档\FNSoftware\MenuManager
        /// </summary>
        public static string UserDataDirectory
        {
            get
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents, VendorName, AppName);
            }
        }

        /// <summary>
        /// 用户配置文件路径
        /// </summary>
        public static string ConfigFilePath => Path.Combine(UserDataDirectory, ConfigFileName);

        /// <summary>
        /// 安装/程序目录（用于读取随程序分发的默认配置）
        /// </summary>
        public static string InstallDirectory =>
            Path.GetDirectoryName(Environment.ProcessPath)
            ?? AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// 确保用户数据目录存在
        /// </summary>
        public static string EnsureUserDataDirectory()
        {
            var directory = UserDataDirectory;
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
