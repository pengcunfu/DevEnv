using System.Diagnostics;
using System.IO;
using System.Windows;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Infrastructure;

/// <summary>少量需要真实窗口 / 系统调用的宿主辅助。</summary>
public static class HostServices
{
    /// <summary>对话框 owner：主窗可见时用它，否则为 null。</summary>
    public static Window? DialogOwner
    {
        get
        {
            var main = Application.Current?.MainWindow;
            return main is { IsVisible: true } ? main : null;
        }
    }

    /// <summary>把一句话写到指定页面所在主窗底部的状态栏。</summary>
    public static void SetStatus(string message, string pageId)
    {
        (WindowForPage(pageId) ?? Application.Current?.MainWindow as MainWindow)
            ?.ViewModel.SetStatus(message);
    }

    /// <summary>写页面级摘要（如网络监控的连接数与速率），页面切换时由页面清空。</summary>
    public static void SetPageStatus(string message, string pageId)
    {
        (WindowForPage(pageId) ?? Application.Current?.MainWindow as MainWindow)
            ?.ViewModel.SetPageStatus(message);
    }

    /// <summary>
    /// 按页面 id 找到承载它的主窗。主窗拆成开发 / 监控两个之后，不能只认
    /// <see cref="Application.MainWindow"/>：隐藏的监控窗口在后台刷新时，
    /// 也必须写回自己的状态栏，而不是写到当时激活的开发窗口上。
    /// </summary>
    private static MainWindow? WindowForPage(string pageId) =>
        Application.Current?.Windows.OfType<MainWindow>()
            .FirstOrDefault(window => string.Equals(
                window.ViewModel.CurrentPageId, pageId, StringComparison.OrdinalIgnoreCase));

    /// <summary>打开资源管理器并定位到指定文件（不存在时只打开所在目录）。</summary>
    public static void OpenFolder(string path)
    {
        try
        {
            var target = System.IO.File.Exists(path) ? path : Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(target))
                return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{target}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开目录：{ex.Message}", "FireNeb DevEnv", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"无法打开链接：{ex.Message}", "FireNeb DevEnv", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static string VersionText
    {
        get
        {
            var version = typeof(MainViewModel).Assembly.GetName().Version;
            return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}