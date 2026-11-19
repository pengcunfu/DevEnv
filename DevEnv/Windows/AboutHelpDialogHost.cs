using System.Windows;

namespace DevEnv.Host.Windows;

/// <summary>「关于与帮助」模态框宿主：单实例，从主窗口菜单打开。</summary>
public static class AboutHelpDialogHost
{
    private static AboutHelpWindow? _window;

    /// <summary>打开关于与帮助；已打开时只激活已有窗口。</summary>
    public static void Open()
    {
        if (_window is { IsVisible: true } active)
        {
            active.Activate();
            return;
        }

        var window = new AboutHelpWindow();
        if (Application.Current?.MainWindow is { IsVisible: true } owner
            && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        _window = window;
        window.Closed += (_, _) => _window = null;
        window.ShowDialog();
    }

    /// <summary>应用退出前关闭模态框，避免阻塞退出流程。</summary>
    public static void Close()
    {
        try { _window?.Close(); } catch { /* 已在退出路径 */ }
        _window = null;
    }
}
