using System.Windows;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 快捷方式启动器宿主：本地应用 / 文件夹 / 网站三页共用一个全屏窗口。
/// 单实例，重复打开是「切到对应页签并前置已有窗口」，不再开一堆。
/// </summary>
public static class ShortcutWindowHost
{
    private static ShortcutsWindow? _window;

    /// <summary>打开（或前置）快捷方式窗口，并切到指定页签：apps / folders / webs。</summary>
    public static void Open(string? pageId = null)
    {
        if (_window is null)
        {
            _window = new ShortcutsWindow();
            _window.Closed += (_, _) => _window = null;
        }

        var window = _window;
        AttachOwner(window);

        window.ShowPage(pageId);
        if (!window.IsVisible)
            window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    /// <summary>
    /// 单独按 Win 键的入口：启动台已打开就收起，否则打开。
    /// 打开前返回 false 表示此刻不该由启动台接手（模态设置框正开着），
    /// 此时不吞 Win 键，让系统开始菜单照常弹出。
    /// </summary>
    public static bool TryToggleFromWinKey()
    {
        if (_window is { IsVisible: true })
        {
            _window.Dismiss();
            return true;
        }

        if (!CanHandleWinKey())
            return false;

        Open();
        return true;
    }

    /// <summary>Win 键手势是否可由启动台接手：模态对话框会禁用其它窗口，这时让给系统。</summary>
    public static bool CanHandleWinKey()
    {
        if (SettingsDialogHost.IsOpen)
            return false;

        var app = Application.Current;
        if (app is null)
            return true;

        // ShowDialog 会话期间被禁用的窗口 IsEnabled == false
        return !app.Windows.OfType<Window>().Any(w => w.IsVisible && !w.IsEnabled);
    }

    /// <summary>主窗恢复显示时，把已打开的快捷方式窗口一起带出来。</summary>
    public static void RefreshAll()
    {
        if (_window is not null)
            AttachOwner(_window);
    }

    /// <summary>主窗真正退出前调用，避免 owned 窗口被 Cancel 影响退出流程。</summary>
    public static void Close()
    {
        var window = _window;
        _window = null;
        if (window is null)
            return;
        try
        {
            window.Close();
        }
        catch
        {
            // 已在退出路径，忽略
        }
    }

    private static void AttachOwner(Window window)
    {
        var main = Application.Current?.MainWindow;
        if (main is not null && !ReferenceEquals(main, window) && window.Owner != main)
            window.Owner = main;
    }
}
