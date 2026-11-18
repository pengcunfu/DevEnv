using System.Windows;
using System.Windows.Threading;
using DevEnv.Core.Services;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 启动器宿主：六个页签（本地应用 / 文件夹 / 网站 / 系统工具 / 内置工具 / 开始菜单）共用同一个居中窗口。
/// 单实例，重复打开是「切到对应页签 / 展开文件夹并前置已有窗口」，不再开一堆。
///
/// 启动器就是产品主窗口，**不设 Owner**：它不跟别的窗口最小化 / 隐藏联动，
/// 从托盘唤起时也不会把别的窗口一起拉出来，所以这里不走 ToolWindowHost 那套 owner 绑定。
/// 启动器本身是普通非置顶窗口；从它打开的工具窗会挂它做 Owner。
/// </summary>
public static class ShortcutWindowHost
{
    /// <summary>收起开始菜单后留一两帧再显示启动器，避免开始菜单压在启动器上面。</summary>
    private static readonly TimeSpan StartMenuDismissDelay = TimeSpan.FromMilliseconds(120);

    private static ShortcutsWindow? _window;
    private static DispatcherTimer? _pendingOpenTimer;
    private static bool _startMenuSyncStarted;

    /// <summary>
    /// 启动后调用一次：在内存里建好窗口并完成布局与首帧渲染（屏幕外、不可见）。
    /// 每次打开都临时创建窗口会让首次显示先闪空白，预热后 Show 只是把窗口摆出来。
    /// 预热完成后顺带在后台补建开始菜单快捷方式并与快速访问对账，不阻塞界面。
    /// </summary>
    public static void Prewarm()
    {
        EnsureWindow().Prewarm();
        StartBackgroundSync();
    }

    /// <summary>开始菜单补建与快速访问同步只跑一次；探针直接创建窗口，不会走到这里。</summary>
    private static void StartBackgroundSync()
    {
        if (_startMenuSyncStarted)
            return;

        _startMenuSyncStarted = true;
        _ = Task.Run(() =>
        {
            try
            {
                CoreServices.Shortcuts.EnsureStartMenuShortcuts();
            }
            catch
            {
                // 开始菜单同步失败不影响启动与启动器本身
            }

            try
            {
                CoreServices.Shortcuts.SyncQuickAccess();
            }
            catch
            {
                // 快速访问同步失败（Shell 不可用等）不影响启动与启动器本身
            }
        });
    }

    /// <summary>当前启动器窗口实例（预热后即存在，可能处于隐藏状态）。</summary>
    public static Window? Window => _window;

    private static ShortcutsWindow EnsureWindow()
    {
        if (_window is not null)
            return _window;

        var window = new ShortcutsWindow();
        window.Closed += (_, _) =>
        {
            var app = Application.Current;
            if (app is not null && ReferenceEquals(app.MainWindow, _window))
                app.MainWindow = null;
            _window = null;
        };
        _window = window;
        BindAsApplicationMainWindow(window);
        return window;
    }

    /// <summary>启动器即产品主窗口：对话框 owner / 二次启动都以它为准。</summary>
    private static void BindAsApplicationMainWindow(Window window)
    {
        var app = Application.Current;
        if (app is null)
            return;
        app.MainWindow = window;
    }

    /// <summary>内容页 id → 独立窗口的 ToolWindowHost id。</summary>
    private static readonly Dictionary<string, string> CategoryWindows = new(StringComparer.OrdinalIgnoreCase)
    {
        ["apps"] = "shortcut-apps",
        ["folders"] = "shortcut-folders",
        ["webs"] = "shortcut-webs",
        ["startmenu"] = "shortcut-startmenu",
    };

    /// <summary>
    /// 打开（或前置）启动器；apps / folders / webs / startmenu 已拆成独立窗口，
    /// 这里转给 <see cref="ToolWindowHost"/>，tools / builtin 仍切启动器自己的页签。
    /// </summary>
    public static void Open(string? pageId = null)
    {
        var normalized = pageId?.Trim().TrimStart('/');
        if (!string.IsNullOrWhiteSpace(normalized)
            && CategoryWindows.TryGetValue(normalized, out var toolId))
        {
            ToolWindowHost.Open(toolId);
            return;
        }

        // 开始菜单是置顶窗口：先把开始菜单收掉，等它真正隐藏后再显示启动器，
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
        BindAsApplicationMainWindow(window);
        if (window.Owner is not null)
            window.Owner = null;
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        // Show / 激活只在 ShowPage 里做一次：这里再补 Show / Activate 会让窗口
        // 被重复激活、重绘，表现为进入时闪一下。
        window.ShowPage(pageId);
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
