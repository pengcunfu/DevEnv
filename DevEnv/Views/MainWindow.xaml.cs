using DevEnv.UI;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using DevEnv.Models;
using DevEnv.Services;
using DevEnv.ViewModels;

namespace DevEnv.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private async void StartProcess_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProcessItemViewModel process })
            await process.StartAsync();
    }

    private async void StopProcess_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProcessItemViewModel process })
            await process.StopAsync();
    }

    private void OpenAppsDir_Click(object? sender, RoutedEventArgs e) => _viewModel.OpenAppsDirectory();

    protected override void OnClosed(EventArgs e)
    {
        try { _viewModel?.StopMonitoring(); } catch { /* ignore */ }
        base.OnClosed(e);
    }

    private void OpenJsonFormatter_Click(object? sender, RoutedEventArgs e)
        => new JsonFormatterWindow().Show(this);

    private void OpenSoftwareDownloader_Click(object? sender, RoutedEventArgs e)
        => OpenSoftwareDownload();

    private void OpenSoftwareDownload_Click(object? sender, RoutedEventArgs e)
        => OpenSoftwareDownload();

    private void OpenSoftwareDownload()
        => new SoftwareDownloadWindow().Show(this);

    private void OpenDownloadManager_Click(object? sender, RoutedEventArgs e)
        => new DownloadManagerWindow().Show(this);

    private void OpenEnvironmentScanner_Click(object? sender, RoutedEventArgs e)
        => new EnvironmentScannerWindow().Show(this);

    private void OpenMirrorConfig_Click(object? sender, RoutedEventArgs e)
        => new MirrorConfigWindow().Show(this);

    private void OpenSettings_Click(object? sender, RoutedEventArgs e)
        => new SettingsWindow().Show(this);

    private void OpenHostsFileEditor_Click(object? sender, RoutedEventArgs e)
    {
        if (ElevationHelper.IsAdministrator())
        {
            new HostsFileEditWindow().Show(this);
            return;
        }

        ElevationHelper.TryRunElevated(ElevationHelper.EditHostsArgument);
    }

    private void OpenImageConverter_Click(object? sender, RoutedEventArgs e)
        => new ImageConverterWindow().Show(this);

    private void OpenHashCalculator_Click(object? sender, RoutedEventArgs e)
        => new HashCalculatorWindow().Show(this);

    private void OpenEnvironmentVariables_Click(object? sender, RoutedEventArgs e)
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

    private void ToolsMenuButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ToolsMenuButton.ContextMenu is { } menu)
            menu.Open(ToolsMenuButton);
    }

    private void OpenDocs_Click(object? sender, TappedEventArgs e)
        => Process.Start(new ProcessStartInfo(AppInfo.DocsUrl) { UseShellExecute = true });

    private void Version_Click(object? sender, TappedEventArgs e) => ShowAbout();

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
            $"• 数据目录: {AppPaths.Root}\n" +
            "• JSON 格式化 / Hosts 编辑 / 哈希计算 / 图像转换\n\n" +
            $"版本: {AppInfo.Version}",
            "关于", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
