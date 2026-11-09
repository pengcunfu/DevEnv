using System.Windows;
using System.Windows.Threading;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 全局启动器宿主：本地应用 / 文件夹 / 网站三个页签共用同一个居中悬浮窗口，
/// 系统工具与内置工具是搜索框右侧的两个只读文件夹。
/// 单实例，重复打开是「切到对应页签 / 展开文件夹并前置已有窗口」，不再开一堆。
///
/// 全局启动器**不设 Owner**：它居中悬浮且置顶，本来就不跟主窗最小化 / 隐藏联动；
/// 一旦挂到主窗名下，从托盘（主窗隐藏）唤起时会把主窗一起拉出来，
/// 所以这里刻意不走 ToolWindowHost 那套 owner 绑定。
/// </summary>
public static class ShortcutWindowHost
{
    /// <summary>失去焦点自动收起后的短时间窗口：在此期间再点托盘菜单视为「关闭」而不是重新打开。</summary>
    private static readonly TimeSpan AutoDismissGrace = TimeSpan.FromMilliseconds(500);

    /// <summary>收起开始菜单后留一两帧再显示启动器，避免两个置顶窗口叠在一起。</summary>
    private static readonly TimeSpan StartMenuDismissDelay = TimeSpan.FromMilliseconds(120);

    private static ShortcutsWindow? _window;
    private static DateTime _lastAutoDismissUtc = DateTime.MinValue;
    private static DispatcherTimer? _pendingOpenTimer;

    /// <summary>
    /// 启动后调用一次：在内存里建好窗口并完成布局与首帧渲染（屏幕外、不可见）。
    /// 每次打开都临时创建窗口会让首次显示先闪空白，预热后 Show 只是把窗口摆出来。
    /// </summary>
    public static void Prewarm() => EnsureWindow().Prewarm();

    private static ShortcutsWindow EnsureWindow()
    {
        if (_window is not null)
            return _window;

        var window = new ShortcutsWindow();
        window.Closed += (_, _) => _window = null;
        window.AutoDismissed += () => _lastAutoDismissUtc = DateTime.UtcNow;
        _window = window;
        return window;
    }

    /// <summary>打开（或前置）全局启动器，并切到指定页签：apps / folders / webs；tools / builtin 直接展开对应工具文件夹。</summary>
    public static void Open(string? pageId = null)
    {
        // 开始菜单和启动器都是置顶窗口：先把开始菜单收掉，等它真正隐藏后再显示启动器，
        // 否则开始菜单会一直压在启动器上面，看起来像 Alt 没生效。
        if (ShellStartMenu.DismissIfOpen())
        {
            ScheduleOpen(pageId);
            return;
        }

        OpenCore(pageId);
    }

    /// <summary>开始菜单最多收几次 Esc：搜索框里有内容时，第一次 Esc 只清空搜索。</summary>
    private const int MaxStartMenuDismissAttempts = 3;

    /// <summary>
    /// 开始菜单收到 Esc 后需要一两帧完成收起。这段延迟期间再次请求打开时，
    /// 取消上一个计时器，用最后一次的页签参数重新计时，避免连续按 Alt 时重复显示。
    /// </summary>
    private static void ScheduleOpen(string? pageId, int attempt = 0)
    {
        CancelPendingOpen();

        // 固定用应用主线程的 Dispatcher 计时，避免从非 UI 线程调用 Open 时计时器永远不跑
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
        {
            Interval = StartMenuDismissDelay,
        };
        _pendingOpenTimer = timer;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!ReferenceEquals(_pendingOpenTimer, timer))
                return;
            _pendingOpenTimer = null;

            // 搜索框里有内容时，一次 Esc 只会清空搜索，开始菜单仍然开着；
            // 再补一两次 Esc，确认它已经收起后才显示启动器。
            if (attempt + 1 < MaxStartMenuDismissAttempts && ShellStartMenu.DismissIfOpen())
            {
                ScheduleOpen(pageId, attempt + 1);
                return;
            }

            OpenCore(pageId);
        };
        timer.Start();
    }

    private static void CancelPendingOpen()
    {
        _pendingOpenTimer?.Stop();
        _pendingOpenTimer = null;
    }

    private static void OpenCore(string? pageId)
    {
        // 直接打开时取消还在等待的延迟任务，避免稍后重复 ShowPage
        CancelPendingOpen();

        var window = EnsureWindow();
        if (window.Owner is not null)
            window.Owner = null;
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        // Show / 激活只在 ShowPage 里做一次：这里再补 Show / Activate 会让窗口
        // 被重复激活、重绘，表现为进入时闪一下。
        window.ShowPage(pageId);
    }

    /// <summary>
    /// 开关全局启动器（左 Alt 键与托盘菜单共用）：已打开就收起，否则打开。
    /// 返回 false 表示此刻不该由全局启动器接手（模态设置框正开着），
    /// 让左 Alt 照常交给前台程序。
    /// </summary>
    public static bool Toggle()
    {
        // 正在淡出时再按一次 = 想重新打开，交给 Open 取消这次收起
        if (_window is { IsVisible: true } window && !window.IsHiding)
        {
            CancelPendingOpen();
            window.Dismiss();
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
        CancelPendingOpen();

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
