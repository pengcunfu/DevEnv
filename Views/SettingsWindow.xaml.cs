using System.Diagnostics;
using System.IO;
using System.Windows;
using DevEnv.Models;
using DevEnv.Services;

namespace DevEnv.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadInfo();
        }

        private void LoadInfo()
        {
            TxtRoot.Text = $"根目录:       {AppPaths.Root}";
            TxtConfig.Text = $"运行时配置:   {AppPaths.ConfigFile}";
            TxtConfigTemplate.Text = $"内置模板:     {ResourcePaths.GetBundledResourcePath(ResourcePaths.DefaultConfigFile)}";
            TxtCache.Text = $"下载缓存:   {AppPaths.CacheDir}";
            TxtApps.Text = $"应用目录:   {AppPaths.AppsDir}";
            TxtHistory.Text = $"下载历史:   {AppPaths.DownloadHistoryFile}";

            var settings = AppServices.Config.Load();
            TxtConfigSummary.Text =
                $"自动解压: {(settings.AutoExtractPortable ? "是" : "否")}\n" +
                $"最大重试: {settings.MaxRetries} 次\n" +
                $"连接超时: {settings.Timeout} 秒\n" +
                $"下载块大小: {settings.ChunkSizeMb} MB\n" +
                $"系统代理: {(settings.UseSystemProxy ? "启用" : "禁用")}\n" +
                $"自定义代理: {(string.IsNullOrWhiteSpace(settings.CustomProxy) ? "无" : settings.CustomProxy)}";
        }

        private void BtnOpenRoot_Click(object sender, RoutedEventArgs e) => OpenPath(AppPaths.Root);

        private void BtnOpenConfig_Click(object sender, RoutedEventArgs e)
        {
            AppServices.Config.Load();
            OpenPath(AppPaths.ConfigFile);
        }

        private static void OpenPath(string path)
        {
            try
            {
                if (File.Exists(path))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                else if (Directory.Exists(path))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                else
                    MessageBox.Show($"路径不存在: {path}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
