using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.ViewModels;
using DevEnv.Host.Views;

namespace DevEnv.Host;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        TryLoadIcon();

        AllowDrop = true;
        DragOver += OnDragOver;
        Drop += OnDrop;
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    /// <summary>页面跳转入口：菜单、系统工具回调、拖放后定位都走这里。</summary>
    public void NavigateTo(string? id) => ViewModel.Navigate(id);

    /// <summary>刷新当前页面（拖放添加快捷方式后调用）。</summary>
    public void ReloadCurrentView()
    {
        if (ViewModel.CurrentView is IReloadableView reloadable)
            reloadable.Reload();
    }

    private void TryLoadIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");
            if (!File.Exists(path))
                return;

            var decoder = new IconBitmapDecoder(
                new Uri(path),
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            // ICO 第一帧是 16px，BitmapImage 默认只取第一帧，任务栏会按 16px 绘制，
            // 看起来比其他图标小一圈。这里取最大帧，由系统按 DPI 缩小。
            Icon = decoder.Frames
                .OrderByDescending(frame => frame.PixelWidth)
                .FirstOrDefault() ?? Icon;
        }
        catch
        {
            // 图标缺失不影响运行
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>把拖进来的 .exe / 文件夹落到全局启动器的对应页签。</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        var addedApps = 0;
        var addedFolders = 0;
        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file))
                continue;

            if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(file))
            {
                if (CoreServices.Shortcuts.AddApp(file).Ok)
                    addedApps++;
            }
            else if (Directory.Exists(file))
            {
                if (CoreServices.Shortcuts.AddFolder(file).Ok)
                    addedFolders++;
            }
        }

        var added = addedApps + addedFolders;
        if (added == 0)
        {
            ViewModel.SetStatus("拖入内容里没有可添加的 exe 或文件夹");
            return;
        }

        ViewModel.SetStatus($"已添加 {added} 项");
        ShortcutWindowHost.Open(addedFolders > 0 && addedApps == 0 ? "folders" : "apps");
    }

    /// <summary>点关闭 = 进托盘而不是退出；只有显式退出才真正关闭。</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
            e.Cancel = app.TryHideToTray();
    }
}