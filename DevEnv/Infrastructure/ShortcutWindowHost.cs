using System.Windows;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 全局启动器宿主：本地应用 / 文件夹 / 网站三页共用一个全屏窗口。
/// 单实例，重复打开是「切到对应页签并前置已有窗口」，不再开一堆。
///
/// 全局启动器**不设 Owner**：它铺满全屏且置顶，本来就不跟主窗最小化 / 隐藏联动；
/// 一旦挂到主窗名下，从托盘（主窗隐藏）唤起时会把主窗一起拉出来，
/// 所以这里刻意不走 ToolWindowHost 那套 owner 绑定。
/// </summary>
public static class ShortcutWindowHost
{
    /// <summary>失去焦点自动收起后的短时间窗口：在此期间再点托盘 / 按 Win 视为「关闭」而不是重新打开。</summary>
    private static readonly TimeSpan AutoDismissGrace = TimeSpan.FromMilliseconds(500);

    private static ShortcutsWindow? _window;
    private static DateTime _lastAutoDismissUtc = DateTime.MinValue;

    /// <summary>打开（或前置）全局启动器，并切到指定页签：apps / folders / webs。</summary>
    public static void Open(string? pageId = null)
    {
        if (_window is null)
        {
            _window = new ShortcutsWindow();
            _window.Closed += (_, _) => _window = null;
            _window.AutoDismissed += () => _lastAutoDismissUtc = DateTime.UtcNow;
        }

        var window = _window;
        if (window.Owner is not null)
            window.Owner = null;

        window.ShowPage(pageId);
        if (!window.IsVisible)
            window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    /// <summary>
    /// 开关全局启动器（Win 键与托盘菜单共用）：已打开就收起，否则打开。
    /// 返回 false 表示此刻不该由全局启动器接手（模态设置框正开着），
    /// Win 键此时不吞键，让系统开始菜单照常弹出。
    /// </summary>
    public static bool Toggle()
    {
        if (_window is { IsVisible: true })
        {
            _window.Dismiss();
            return true;
        }

        // 点托盘 / 按 Win 时全局启动器会先因失去焦点自动收起，紧接着的这次调用应视为「关闭」，
        // 否则会收起后立刻又弹出来，看起来像点了没反应。
        if (DateTime.UtcNow - _lastAutoDismissUtc < AutoDismissGrace)
            return true;

        if (!CanOpen())
            return false;

        Open();
        return true;
    }

    /// <summary>此刻能否打开全局启动器：模态对话框会禁用其它窗口，这时让给系统开始菜单。</summary>
    public static bool CanOpen()
    {
        if (SettingsDialogHost.IsOpen)
            return false;

        var app = Application.Current;
        if (app is null)
            return true;

        // ShowDialog 会话期间被禁用的窗口 IsEnabled == false
        return !app.Windows.OfType<Window>().Any(w => w.IsVisible && !w.IsEnabled);
    }

    /// <summary>主窗真正退出前调用，避免退出流程被未关闭的窗口阻塞。</summary>
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
}
