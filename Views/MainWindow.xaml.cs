using System.Windows;
using System.Windows.Controls;
using DevEnv.ViewModels;
using DevEnv.Views;

namespace DevEnv.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;
        }

        private async void StartService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ServiceItemViewModel service)
            {
                await service.StartServiceAsync();
            }
        }

        private async void StopService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ServiceItemViewModel service)
            {
                await service.StopServiceAsync();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _viewModel?.StopMonitoring();
            }
            catch
            {
            }
            base.OnClosed(e);
        }

        private void OpenJsonFormatter_Click(object sender, RoutedEventArgs e)
        {
            var jsonFormatter = new JsonFormatterWindow();
            jsonFormatter.ShowDialog();
        }

        private void OpenSoftwareDownloader_Click(object sender, RoutedEventArgs e)
        {
            var downloader = new SoftwareDownloadWindow();
            downloader.ShowDialog();
        }

        private void OpenHostsFileEditor_Click(object sender, RoutedEventArgs e)
        {
            var hostsEditor = new HostsFileEditWindow();
            hostsEditor.ShowDialog();
        }

        private void OpenImageConverter_Click(object sender, RoutedEventArgs e)
        {
            var imageConverter = new ImageConverterWindow();
            imageConverter.ShowDialog();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Windows 服务管理器\n\n包含功能：\n• 服务管理\n• JSON 格式化工具\n• 软件下载工具\n• Hosts 文件编辑器\n• 图像格式转换器\n\n版本: 1.0.0", "关于", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
