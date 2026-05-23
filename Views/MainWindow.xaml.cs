using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Services;
using DevEnv.ViewModels;

namespace DevEnv.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }

        private async void StartProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProcessItemViewModel process)
                await process.StartAsync();
        }

        private async void StopProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProcessItemViewModel process)
                await process.StopAsync();
        }

        private void OpenAppsDir_Click(object sender, RoutedEventArgs e) => _viewModel.OpenAppsDirectory();

        protected override void OnClosed(EventArgs e)
        {
            try { _viewModel?.StopMonitoring(); } catch { }
            base.OnClosed(e);
        }

        private void OpenJsonFormatter_Click(object sender, RoutedEventArgs e)
        {
            new JsonFormatterWindow().ShowDialog();
        }

        private void OpenSoftwareDownloader_Click(object sender, RoutedEventArgs e)
        {
            var window = new SoftwareDownloadWindow { Owner = this };
            window.Show();
        }

        private void OpenDownloadManager_Click(object sender, RoutedEventArgs e)
        {
            var window = new DownloadManagerWindow { Owner = this };
            window.Show();
        }

        private void OpenEnvironmentScanner_Click(object sender, RoutedEventArgs e)
        {
            var window = new EnvironmentScannerWindow { Owner = this };
            window.Show();
        }

        private void OpenMirrorConfig_Click(object sender, RoutedEventArgs e)
        {
            var window = new MirrorConfigWindow { Owner = this };
            window.Show();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            new SettingsWindow().ShowDialog();
        }

        private void OpenHostsFileEditor_Click(object sender, RoutedEventArgs e)
        {
            if (ElevationHelper.IsAdministrator())
            {
                new HostsFileEditWindow { Owner = this }.ShowDialog();
                return;
            }

            ElevationHelper.TryRunElevated(ElevationHelper.EditHostsArgument);
        }

        private void OpenImageConverter_Click(object sender, RoutedEventArgs e)
        {
            new ImageConverterWindow().ShowDialog();
        }

        private void OpenHashCalculator_Click(object sender, RoutedEventArgs e)
        {
            new HashCalculatorWindow().ShowDialog();
        }

        private void OpenEnvironmentVariables_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemTools.OpenEnvironmentVariables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开环境变量设置: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToolsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsMenuButton.ContextMenu is { } menu)
            {
                menu.PlacementTarget = ToolsMenuButton;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void OpenDocs_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo(AppInfo.DocsUrl) { UseShellExecute = true });
        }

        private void Version_Click(object sender, MouseButtonEventArgs e) => ShowAbout();

        private void ShowAbout()
        {
            MessageBox.Show(
                $"{AppInfo.ProductName}\n\n" +
                $"{AppInfo.ProductTagline}\n\n" +
                "核心功能：\n" +
                "• 绿色版进程管理 (MySQL/PostgreSQL/Redis/MongoDB/MinIO/Nginx)\n" +
                "• 软件下载与自动解压 (zip 绿色包)\n" +
                "• 环境扫描 (Java/Python/Node.js/Go/PHP/.NET)\n" +
                "• 镜像源配置 (pip/npm/pnpm/Yarn/Maven/Composer/Go/NuGet)\n" +
                "• 数据目录: D:\\devenv (config.json 配置)\n" +
                "• JSON 格式化 / Hosts 编辑 / 哈希计算 / 图像转换\n\n" +
                $"版本: {AppInfo.Version}",
                "关于", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
