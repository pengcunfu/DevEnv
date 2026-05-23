using System.Windows;
using System.Windows.Controls;
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
            new HostsFileEditWindow().ShowDialog();
        }

        private void OpenImageConverter_Click(object sender, RoutedEventArgs e)
        {
            new ImageConverterWindow().ShowDialog();
        }

        private void OpenHashCalculator_Click(object sender, RoutedEventArgs e)
        {
            new HashCalculatorWindow().ShowDialog();
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "DevEnv - 开发环境管理器\n\n" +
                "核心理念：绿色版 · 免安装 · 无需管理员权限\n\n" +
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
