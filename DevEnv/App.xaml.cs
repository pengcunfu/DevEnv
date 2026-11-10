using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DevEnv.Core;
using DevEnv.Core.Input;
using DevEnv.Core.Services;
using DevEnv.Core.LavaEnv.Services;
using DevEnv.Core.Updates;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.SigXor;
using DevEnv.Host.SigXor.Diagnostics;
using DevEnv.Host.Themes;
using DevEnv.Host.ViewModels;
using DevEnv.Host.Views;
using DevEnv.Host.Windows;
using Forms = System.Windows.Forms;
using WpfClipboard = System.Windows.Clipboard;

namespace DevEnv.Host;

/// <summary>
/// 应用入口：单实例、托盘、全局热键、剪贴板轮询都在这里装配。
/// 关闭主窗只是隐藏（进托盘），只有显式退出或 Velopack 更新才真正 Shutdown。
/// </summary>
public partial class App : System.Windows.Application
{
    private const string DevelopmentWindowTitle = "FireNeb DevEnv";
    private const string MonitorWindowTitle = "FireNeb Monitor";

    /// <summary>开发窗口：开发环境 / 熔岩环境，启动时创建（隐藏驻留托盘）。</summary>
    private MainWindow? _devWindow;

    /// <summary>监控窗口：网络 / 进程 / 服务监控，第一次需要时才创建。</summary>
    private MainWindow? _monitorWindow;

    private TrayIconHost? _tray;
    private GlobalHotkeys? _hotkeys;
    private HwndSourceHook? _hotkeyHook;
    private ClipboardWatcher? _clipboardWatcher;
    private AltKeyLauncherHook? _altKeyHook;
    private TranslatorWindow? _translatorWindow;
    private ClipboardWindow? _clipboardWindow;
    private SigXorHost? _sigXor;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;
        // 后台线程异常不走 Dispatcher，之前会静默结束进程，这里也落盘一份。
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrashLog(args.ExceptionObject as Exception, "AppDomain");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrashLog(args.Exception, "Task");
            args.SetObserved();
        };

        // SigXor 模块自检探针：独立进程跑完检查即退出，不进单例、不建窗口。
        // 用法见 SigXorDiagnostics 的说明。
        int checkIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, SigXorDiagnostics.Argument, StringComparison.OrdinalIgnoreCase));
        if (checkIndex >= 0)
        {
            string report = checkIndex + 1 < e.Args.Length ? e.Args[checkIndex + 1] : "";
            RunSigXorDiagnosticsAsync(report);
            return;
        }

        int smokeIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, SigXorSmokeProbe.Argument, StringComparison.OrdinalIgnoreCase));
        if (smokeIndex >= 0)
        {
            string report = smokeIndex + 1 < e.Args.Length ? e.Args[smokeIndex + 1] : "";
            Shutdown(SigXorSmokeProbe.Run(
                string.IsNullOrWhiteSpace(report) ? [] : [report]));
            return;
        }

        int previewIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, SigXorPreviewProbe.Argument, StringComparison.OrdinalIgnoreCase));
        if (previewIndex >= 0)
        {
            string report = previewIndex + 1 < e.Args.Length ? e.Args[previewIndex + 1] : "";
            RunSigXorPreviewProbeAsync(report);
            return;
        }

        int themeIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, SigXorThemeProbe.Argument, StringComparison.OrdinalIgnoreCase));
        if (themeIndex >= 0)
        {
            string report = themeIndex + 1 < e.Args.Length ? e.Args[themeIndex + 1] : "";
            Shutdown(SigXorThemeProbe.Run(
                string.IsNullOrWhiteSpace(report) ? [] : [report]));
            return;
        }

        // 合并后的设置对话框探针：验证「常规」「快捷键」「语音与截屏」「服务与模型」四个页签
        int settingsIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, SettingsDialogProbe.Argument, StringComparison.OrdinalIgnoreCase));
        if (settingsIndex >= 0)
        {
            string report = settingsIndex + 1 < e.Args.Length ? e.Args[settingsIndex + 1] : "";
            Shutdown(SettingsDialogProbe.Run(
                string.IsNullOrWhiteSpace(report) ? [] : [report]));
            return;
        }

        // 左 Alt 拦截自检：真实安装钩子 + SendInput 模拟按键，验证触发与放行
        int altKeyIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, AltKeyLauncherProbe.Argument, StringComparison.OrdinalIgnoreCase));
        if (altKeyIndex >= 0)
        {
            string report = altKeyIndex + 1 < e.Args.Length ? e.Args[altKeyIndex + 1] : "";
            Shutdown(AltKeyLauncherProbe.Run(string.IsNullOrWhiteSpace(report) ? [] : [report]));
            return;
        }

        // 全局启动器自检：三个页签与两个工具文件夹能加载、搜索可用、没有数据绑定错误
        int shortcutsIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, ShortcutLauncherProbe.Argument, StringComparison.OrdinalIgnoreCase));
        if (shortcutsIndex >= 0)
        {
            string report = shortcutsIndex + 1 < e.Args.Length ? e.Args[shortcutsIndex + 1] : "";
            Shutdown(ShortcutLauncherProbe.Run(string.IsNullOrWhiteSpace(report) ? [] : [report]));
            return;
        }

        // 主窗口拆分自检：开发窗口与监控窗口各自加载、导航互不影响
        int windowsIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, MainWindowProbe.Argument, StringComparison.OrdinalIgnoreCase));
        if (windowsIndex >= 0)
        {
            string report = windowsIndex + 1 < e.Args.Length ? e.Args[windowsIndex + 1] : "";
            Shutdown(MainWindowProbe.Run(string.IsNullOrWhiteSpace(report) ? [] : [report]));
            return;
        }

        // 右键菜单管理提权子进程：执行完立即退出（绕过单实例）
        var applyIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, ContextMenuService.ElevatedArgument, StringComparison.OrdinalIgnoreCase));
        if (applyIndex >= 0 && applyIndex + 1 < e.Args.Length)
        {
            ContextMenuService.ExecuteElevatedRequest(e.Args[applyIndex + 1]);
            Shutdown();
            return;
        }

        // 管理员编辑 Hosts：绕过单实例，独立小窗。
        // 直接以普通权限运行 --edit-hosts 时先自我提权，保证编辑器启动即具备写权限。
        if (e.Args.Any(a => string.Equals(a, ElevationHelper.EditHostsArgument, StringComparison.OrdinalIgnoreCase)))
        {
            if (!ElevationHelper.IsAdministrator())
            {
                if (!ElevationHelper.TryRunElevated(ElevationHelper.EditHostsArgument))
                {
                    System.Windows.MessageBox.Show(
                        "Hosts 文件编辑器需要管理员权限，已取消提权，编辑器不会打开。",
                        "需要管理员权限",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                Shutdown();
                return;
            }

            var hostsWindow = new HostsEditWindow();
            hostsWindow.Closed += (_, _) => Shutdown();
            hostsWindow.Show();
            return;
        }

        // 命令行里带文件路径：转给主实例弹预览（文件关联 / 右键「打开方式」都走这里）
        var fileArgs = e.Args
            .Where(a => !a.StartsWith("--", StringComparison.Ordinal))
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Where(File.Exists)
            .ToArray();

        if (!SingleInstance.TryStartAsPrimary())
        {
            // 二次启动：把文件路径转给主实例后退出
            if (fileArgs.Length > 0)
                SingleInstance.ForwardFilesToPrimary(fileArgs);
            Shutdown();
            return;
        }

        CoreServices.Initialize();
        AutostartHelper.MigrateLegacyNames();
        ThemeManager.ApplyFromSettings();
        WireHostCallbacks();

        // 启动时不显示任何窗口，只驻留托盘。主窗口 = 全局启动器（预热后挂到
        // Application.MainWindow）；开发窗口仍提前创建以挂热键句柄，但保持隐藏。
        // 监控窗口在第一次从启动器内置工具页进入时才创建。
        _devWindow = CreateMainWindow(DevelopmentWindowTitle, ViewFactory.DevelopmentNavItems);
        EnsureHotkeys(_devWindow);

        SingleInstance.StartActivateListener(Dispatcher, ShowMainWindow);
        SingleInstance.StartFileListener(Dispatcher, paths => _sigXor?.ShowPreviewForPaths(paths));

        _tray = new TrayIconHost("FireNeb DevEnv", LoadAppIcon());
        // 托盘事件在 WinForms 消息循环里触发，统一经 Dispatcher 回 UI 线程
        // 单击托盘 / 「显示主窗口」= 打开启动器（主窗口）
        _tray.MainWindowRequested += () => Dispatcher.InvokeAsync(ShowMainWindow);
        _tray.SettingsRequested += () => Dispatcher.InvokeAsync(() => SettingsDialogHost.Open());
        _tray.TranslatorRequested += () => Dispatcher.InvokeAsync(() => ShowTranslatorWindow(new TranslatorRequest { Visible = true }));
        _tray.ClipboardRequested += () => Dispatcher.InvokeAsync(() => ShowClipboardWindow(true));
        _tray.ExitRequested += ExitApp;

        // SigXor 模块：文件快速预览 / 语音输入 / 区域截图 OCR 取色 / 文件浏览器
        _sigXor = new SigXorHost(Dispatcher);
        _sigXor.NotificationRequested += (title, message) =>
            Dispatcher.BeginInvoke(new Action(() => _tray?.ShowBalloon(message, Forms.ToolTipIcon.Info)));
        _sigXor.Initialize();

        // 设置对话框的分区内容由 SigXor 模块提供
        SettingsDialogHost.Sections = _sigXor?.CreateSettingsSections();

        _tray.FilePreviewRequested += () => Dispatcher.InvokeAsync(() => ShowFilePreviewDialog());
        _tray.ScreenshotRequested += () => Dispatcher.InvokeAsync(() => _sigXor?.StartCapture());
        _tray.SigXorBrowserRequested += () => Dispatcher.InvokeAsync(() => _sigXor?.ShowBrowser());

        // 首次启动就带了文件路径：直接弹预览浮窗
        if (fileArgs.Length > 0)
        {
            Dispatcher.BeginInvoke(new Action(() => _sigXor?.ShowPreviewForPaths(fileArgs)),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        _clipboardWatcher = new ClipboardWatcher();
        _clipboardWatcher.Captured += OnClipboardCaptured;

        // 启动后在后台空闲时把全局启动器（主窗口）准备好（屏幕外完成布局与首帧渲染），
        // 之后左 Alt / 托盘 / Ctrl+Alt+D 唤起时只是把窗口摆出来，不再临时创建、不会闪。
        // 放到 ApplicationIdle 是为了不拖慢启动本身。
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ShortcutWindowHost.Prewarm();
            if (ShortcutWindowHost.Window is { } launcher)
                MainWindow = launcher;
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // 调试用：DEVENV_START_PAGE=lava / network 直接定位到对应窗口的指定页，
        // 启动器页签（apps / tools 等）仍由 MainViewModel.Navigate 转给全局启动器。
        var startPage = Environment.GetEnvironmentVariable("DEVENV_START_PAGE");
        if (!string.IsNullOrWhiteSpace(startPage))
        {
            var pageId = startPage.Trim();
            Dispatcher.BeginInvoke(
                new Action(() => NavigateToMainPage(pageId)),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // 左 Alt 拦截放在启动流程最后安装：前面的探针 / 提权子进程早已 return，
        // 不会把键盘钩子带进那些只跑一次就退出的进程。
        ApplyAltKeyLauncher(CoreServices.Settings.GetConfig().AltKeyLauncher);

        CompleteStartupForUpdate();
    }

    /// <summary>
    /// 更新器重启后的收尾：新版启动成功时写 <c>.update_ok</c> 让看门狗确认，并提示更新/回滚结果。
    /// 普通启动则清掉上次残留的标记。
    /// </summary>
    private void CompleteStartupForUpdate()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(UpdateStartupHooks.UpdatedTo))
            {
                File.WriteAllText(UpdatePaths.MarkerPath, DateTime.UtcNow.ToString("O"));
                _tray?.ShowBalloon($"已更新到 v{UpdateStartupHooks.UpdatedTo}", Forms.ToolTipIcon.Info);
                return;
            }

            if (UpdateStartupHooks.ManualRollback)
            {
                _tray?.ShowBalloon("已手动回滚到上一版本", Forms.ToolTipIcon.Info);
                return;
            }

            if (!string.IsNullOrWhiteSpace(UpdateStartupHooks.RolledBackTo))
            {
                _tray?.ShowBalloon(
                    $"更新失败，已回滚到 v{UpdateStartupHooks.RolledBackTo}",
                    Forms.ToolTipIcon.Warning);
                return;
            }

            if (File.Exists(UpdatePaths.MarkerPath))
                File.Delete(UpdatePaths.MarkerPath);
        }
        catch
        {
            // 标记写入失败只影响看门狗判定，不影响正常启动。
        }
    }

    /// <summary>
    /// 按设置安装 / 卸载左 Alt 拦截钩子。
    /// 开启时单独按左 Alt 键唤起全局启动器，Alt+组合键原样放行。
    /// </summary>
    private void ApplyAltKeyLauncher(bool enabled)
    {
        _altKeyHook?.Resume();

        if (!enabled)
        {
            _altKeyHook?.Uninstall();
            return;
        }

        if (_altKeyHook is null)
        {
            _altKeyHook = new AltKeyLauncherHook(Dispatcher)
            {
                // 模态对话框开着时不抢左 Alt，让前台程序照常收到 Alt
                CanTrigger = ShortcutWindowHost.CanOpen,
            };
            _altKeyHook.LauncherRequested += () => ShortcutWindowHost.Toggle();
        }

        if (!_altKeyHook.Install())
        {
            _tray?.ShowBalloon("左 Alt 拦截失败：无法安装键盘钩子，左 Alt 不会唤起全局启动器。",
                Forms.ToolTipIcon.Warning);
        }
    }

    /// <summary>打开设置对话框的指定页签（菜单 / 托盘 / 系统工具入口共用）。</summary>
    private void OpenSigXorSettingsTab(SettingsSection section)
    {
        _sigXor?.OpenSettings(section);
    }

    /// <summary>托盘「文件快速预览…」：选择文件后弹出预览浮窗。</summary>
    private void ShowFilePreviewDialog()
    {
        var dlg = new Forms.OpenFileDialog
        {
            Title = "选择要预览的文件",
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() != Forms.DialogResult.OK)
            return;
        _sigXor?.ShowPreviewForPaths(dlg.FileNames);
    }

    #region 全局异常

    /// <summary>SigXor 预览链路探针的异步执行入口：跑完写报告文件再退出进程。</summary>
    private async void RunSigXorPreviewProbeAsync(string reportPath)
    {
        int exitCode = 1;
        try
        {
            exitCode = await SigXorPreviewProbe.RunAsync(
                string.IsNullOrWhiteSpace(reportPath) ? [] : [reportPath]);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(reportPath, $"FATAL {ex}"); } catch { }
        }

        Shutdown(exitCode);
    }

    /// <summary>SigXor 全量自检的异步执行入口：跑完写报告文件再退出进程。</summary>
    private async void RunSigXorDiagnosticsAsync(string reportPath)
    {
        int exitCode = 1;
        try
        {
            exitCode = await SigXorDiagnostics.RunAllAsync(
                string.IsNullOrWhiteSpace(reportPath) ? [] : [reportPath]);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(reportPath, $"FATAL {ex}"); } catch { }
        }

        Shutdown(exitCode);
    }

    /// <summary>未处理异常落盘到 %LOCALAPPDATA%\DevEnv\crash.log：桌面程序崩了用户看不到控制台。</summary>
    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception, "Dispatcher");
        e.Handled = true;
    }

    private static void WriteCrashLog(Exception? exception, string source)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevEnv");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 记录失败就算了
        }
    }

    #endregion

    #region 主窗与托盘

    private MainWindow CreateMainWindow(string title, IReadOnlyList<NavItem> navItems)
    {
        // Application.MainWindow 固定为启动器，开发 / 监控窗只作功能窗，不抢主窗口身份
        return new MainWindow(title, navItems) { ShowInTaskbar = false };
    }

    /// <summary>监控窗口按需创建：没人打开时不必带着网络 / 进程监控页的定时器。</summary>
    private MainWindow EnsureMonitorWindow() =>
        _monitorWindow ??= CreateMainWindow(MonitorWindowTitle, ViewFactory.MonitorNavItems);

    /// <summary>最近显示的宿主窗口，翻译窗 / 剪贴板窗没有 owner 时挂到它名下。</summary>
    private MainWindow? VisibleMainWindow =>
        _devWindow is { IsVisible: true } ? _devWindow
        : _monitorWindow is { IsVisible: true } ? _monitorWindow
        : null;

    /// <summary>托盘「显示主窗口」/ 单击托盘 / Ctrl+Alt+D / 二次启动：打开启动器（产品主窗口）。</summary>
    private void ShowMainWindow() => ShortcutWindowHost.Open();

    /// <summary>启动器「内置工具」里的开发窗口入口。</summary>
    private void ShowDevelopmentWindow() => ShowWindow(_devWindow);

    private void ShowMonitorWindow() => ShowWindow(EnsureMonitorWindow());

    private void ShowWindow(MainWindow? window)
    {
        if (window is null)
            return;
        ToolWindowHost.RefreshAll();
        AttachAuxiliaryWindows(window);
        // ShowInTaskbar 要在 Show() 之前设，否则隐藏到托盘后再改会闪一下任务栏按钮
        window.ShowInTaskbar = true;
        if (!window.IsVisible)
            window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
        // 托盘点击不一定会把前台权限交给本进程：先置顶再取消，强制拿到焦点。
        // 取消置顶要延后一拍，立刻改会被系统当成同一次布局而吞掉。
        window.Topmost = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!window.IsVisible)
                return;
            window.Topmost = false;
        }), System.Windows.Threading.DispatcherPriority.Background);
        window.Focus();
    }

    /// <summary>按页面 id 路由到所属窗口：网络 / 进程 / 服务监控进监控窗口，其余走开发窗口。</summary>
    private void NavigateToMainPage(string? pageId)
    {
        if (ViewFactory.IsMonitorPage(pageId))
        {
            EnsureMonitorWindow().NavigateTo(pageId);
            return;
        }

        _devWindow?.NavigateTo(pageId);
    }

    /// <summary>
    /// 主窗显示后，把启动时主窗隐藏期间创建、因而没挂 Owner 的翻译 / 剪贴板窗
    /// 补挂到主窗名下（owner 隐藏时 owned 窗口不会显示，所以当时只能不挂）。
    /// </summary>
    private void AttachAuxiliaryWindows(MainWindow owner)
    {
        if (_translatorWindow is { Owner: null } translator)
            translator.Owner = owner;
        if (_clipboardWindow is { Owner: null } clipboard)
            clipboard.Owner = owner;
    }

    /// <summary>关闭主窗 → 进托盘而不是退出；返回 true 表示已隐藏（调用方应取消关闭）。</summary>
    internal bool TryHideToTray(MainWindow window)
    {
        if (_exiting)
            return false;
        window.Hide();
        window.ShowInTaskbar = false;
        if (_tray is not null && !_tray.Visible)
            _tray.Visible = true;
        return true;
    }

    private void ExitApp()
    {
        if (_exiting)
            return;
        _exiting = true;

        _clipboardWatcher?.Dispose();
        _clipboardWatcher = null;
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        if (_hotkeyHook is not null && _hotkeys is not null && _devWindow is not null
            && HwndSource.FromHwnd(new WindowInteropHelper(_devWindow).Handle) is { } source)
        {
            source.RemoveHook(_hotkeyHook);
        }
        _hotkeyHook = null;
        _hotkeys?.Dispose();
        _hotkeys = null;

        // 先摘键盘钩子：退出路径上继续拦截 Win 键会把开始菜单吞掉
        _altKeyHook?.Dispose();
        _altKeyHook = null;

        ToolWindowHost.CloseAll();
        ShortcutWindowHost.Close();
        SettingsDialogHost.Close();
        _translatorWindow?.PrepareForExit();
        _clipboardWindow?.PrepareForExit();
        try { _translatorWindow?.Close(); } catch { /* 已在退出路径 */ }
        try { _clipboardWindow?.Close(); } catch { /* 已在退出路径 */ }
        try { _sigXor?.Dispose(); } catch { /* 已在退出路径 */ }
        try { _devWindow?.Close(); } catch { /* 已在退出路径 */ }
        try { _monitorWindow?.Close(); } catch { /* 已在退出路径 */ }
        Shutdown();
    }

    private static System.Drawing.Icon? LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");
        try
        {
            return File.Exists(path) ? new System.Drawing.Icon(path) : null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region 全局热键

    private void EnsureHotkeys(MainWindow window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        if (_hotkeys is null)
        {
            _hotkeys = new GlobalHotkeys(hwnd);
            _hotkeys.ShowWindowPressed += ShowMainWindow;
            _hotkeys.ClipboardPressed += () => ShowClipboardWindow(true);
            _hotkeys.TranslatorPressed += () =>
                ShowTranslatorWindow(new TranslatorRequest { Visible = true, FillClipboard = true });
        }
        else
        {
            _hotkeys.Rebind(hwnd);
        }

        _hotkeyHook = (IntPtr _, int msg, IntPtr wParam, IntPtr _, ref bool handled) =>
        {
            if (_hotkeys!.HandleMessage(msg, wParam))
                handled = true;
            return IntPtr.Zero;
        };
        if (HwndSource.FromHwnd(hwnd) is { } source)
            source.AddHook(_hotkeyHook);

        ApplyGlobalHotkeys(includeAltKey: false);
    }

    /// <summary>
    /// 读取 data.json 并注册全局热键。录制快捷键结束后也走这里恢复注册。
    /// </summary>
    private void ApplyGlobalHotkeys(bool notify = true, bool includeAltKey = true)
    {
        string show = "Ctrl+Alt+D";
        string clipboard = "Ctrl+Space";
        string translator = "Alt+Space";
        bool altKeyLauncher = true;
        try
        {
            var cfg = CoreServices.Settings.GetConfig();
            if (!string.IsNullOrWhiteSpace(cfg.ShowWindowHotkey))
                show = cfg.ShowWindowHotkey;
            if (!string.IsNullOrWhiteSpace(cfg.ClipboardHotkey))
                clipboard = cfg.ClipboardHotkey;
            if (!string.IsNullOrWhiteSpace(cfg.TranslatorHotkey))
                translator = cfg.TranslatorHotkey;
            altKeyLauncher = cfg.AltKeyLauncher;
        }
        catch
        {
            // 配置损坏时使用默认值
        }

        if (_hotkeys is not null)
        {
            if (!_hotkeys.ApplyShowWindowHotkey(show) && notify)
                ShowHotkeyFailure(show, "显示主窗口");
            if (!_hotkeys.ApplyClipboardHotkey(clipboard) && notify)
                ShowHotkeyFailure(clipboard, "熔岩剪贴板");
            if (!_hotkeys.ApplyTranslatorHotkey(translator) && notify)
                ShowHotkeyFailure(translator, "熔岩翻译");
        }

        if (includeAltKey)
            ApplyAltKeyLauncher(altKeyLauncher);
    }

    private void ShowHotkeyFailure(string hotkey, string action)
    {
        Dispatcher.BeginInvoke(new Action(() =>
            _tray?.ShowBalloon($"快捷键 {hotkey}（{action}）注册失败，可能已被其它程序占用或格式无效。",
                Forms.ToolTipIcon.Warning)));
    }

    /// <summary>录制快捷键期间挂起全局热键；结束后恢复。</summary>
    private void OnHotkeyCaptureChanged(bool capturing)
    {
        void Apply()
        {
            if (capturing)
            {
                _hotkeys?.Suspend();
                _altKeyHook?.Suspend();
                return;
            }

            _hotkeys?.Resume();
            ApplyGlobalHotkeys(notify: false);
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.BeginInvoke(new Action(Apply));
    }

    /// <summary>唤起 / 隐藏翻译窗（WPF 外壳 + WebView2）。</summary>
    internal void ShowTranslatorWindow(TranslatorRequest request)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _translatorWindow ??= CreateTranslatorWindow();
            if (!request.Visible)
            {
                _translatorWindow.HideAndRelease();
                return;
            }
            _translatorWindow.ShowAndActivate();
            _translatorWindow.Apply(request);
        }));
    }

    /// <summary>唤起 / 隐藏剪贴板窗（纯 WPF）。</summary>
    internal void ShowClipboardWindow(bool visible)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _clipboardWindow ??= CreateClipboardWindow();
            if (visible)
                _clipboardWindow.ShowAndActivate();
            else
                _clipboardWindow.Hide();
        }));
    }

    private TranslatorWindow CreateTranslatorWindow()
    {
        var window = new TranslatorWindow();
        // 只有主窗可见时才挂 Owner：owner 隐藏时 owned 窗口也不会显示
        if (VisibleMainWindow is { } owner)
            window.Owner = owner;
        if (Dispatcher.CheckAccess() || _tray is null)
            return window;

        // 环境创建是异步的，共享 Environment 让浏览器进程复用
        _ = Dispatcher.InvokeAsync(async () =>
        {
            var env = await WebView.WebView2EnvironmentFactory.GetSharedAsync();
            window.AttachEnvironment(env);
        });
        return window;
    }

    private ClipboardWindow CreateClipboardWindow()
    {
        var window = new ClipboardWindow();
        if (VisibleMainWindow is { } owner)
            window.Owner = owner;
        return window;
    }

    #endregion

    #region 宿主回调（核心层 → UI 的唯一接缝）

    private void WireHostCallbacks()
    {
        HostCallbacks.OnTrayIconChanged += visible =>
            Dispatcher.BeginInvoke(new Action(() => _tray!.Visible = visible));
        HostCallbacks.OnGlobalHotkeysChanged += () =>
            Dispatcher.BeginInvoke(new Action(() => ApplyGlobalHotkeys()));
        HotkeyCaptureState.Changed += OnHotkeyCaptureChanged;
        HostCallbacks.OnExitApp += () => Dispatcher.BeginInvoke(ExitApp);
        // 更新时也要走完整的退出路径：先释放托盘 / 钩子 / 窗口，更新器才能拿到 exe 锁。
        HostCallbacks.OnBeforeExitForUpdate += () => Dispatcher.BeginInvoke(ExitApp);
        HostCallbacks.SetClipboardText += text =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { WpfClipboard.SetText(text); } catch { /* 被占用 */ }
            }));
        HostCallbacks.SetClipboardImagePath += path =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(path);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    WpfClipboard.SetImage(bitmap);
                }
                catch
                {
                    // ignore
                }
            }));
        HostCallbacks.RestartElevated += asAdmin => Dispatcher.BeginInvoke(new Action(() => RestartProcess(asAdmin)));
        HostCallbacks.PickExecutable = () =>
        {
            using var dlg = new Forms.OpenFileDialog { Filter = "可执行文件 (*.exe)|*.exe", Multiselect = false };
            return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.FileName : null;
        };
        HostCallbacks.PickFolder = () =>
        {
            using var dlg = new Forms.FolderBrowserDialog();
            return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.SelectedPath : null;
        };
        HostCallbacks.PickFiles = filter =>
        {
            using var dlg = new Forms.OpenFileDialog
            {
                Filter = string.IsNullOrWhiteSpace(filter) ? "所有文件|*.*" : filter,
                Multiselect = true,
            };
            return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.FileNames : null;
        };
        HostCallbacks.SaveFile = (suggested, filter) =>
        {
            using var dlg = new Forms.SaveFileDialog
            {
                FileName = suggested,
                Filter = string.IsNullOrWhiteSpace(filter) ? "所有文件|*.*" : filter,
            };
            return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.FileName : null;
        };
        HostCallbacks.OnActivateTab += tab => Dispatcher.BeginInvoke(new Action(() =>
        {
            // 系统工具目录里的内置工具（tools/json 等）改为开独立窗口
            var tool = tab?.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) == true
                ? tab["tools/".Length..]
                : null;
            if (tool is not null && ToolWindowHost.IsTool(tool))
            {
                ToolWindowHost.Open(tool);
                return;
            }
            NavigateToMainPage(tab);
        }));
        HostCallbacks.OnShowTranslator += request => Dispatcher.BeginInvoke(new Action(() => ShowTranslatorWindow(request)));
        HostCallbacks.OnShowClipboard += visible => Dispatcher.BeginInvoke(new Action(() => ShowClipboardWindow(visible)));
        HostCallbacks.OnShowMainWindow += () => Dispatcher.BeginInvoke(new Action(ShowMainWindow));
        HostCallbacks.OnShowDevelopmentWindow += () => Dispatcher.BeginInvoke(new Action(ShowDevelopmentWindow));
        HostCallbacks.OnShowMonitorWindow += () => Dispatcher.BeginInvoke(new Action(ShowMonitorWindow));

        // SigXor 模块接线：核心层与工具目录通过 HostCallbacks 触发模块能力
        HostCallbacks.OnOpenFilePreview += () => Dispatcher.BeginInvoke(new Action(ShowFilePreviewDialog));
        HostCallbacks.OnOpenScreenshot += () => Dispatcher.BeginInvoke(new Action(() => _sigXor?.StartCapture()));
        HostCallbacks.OnOpenSigXorSettings += section => Dispatcher.BeginInvoke(new Action(() => OpenSigXorSettingsTab(section)));
        HostCallbacks.OnOpenSigXorBrowser += () => Dispatcher.BeginInvoke(new Action(() => _sigXor?.ShowBrowser()));
        HostCallbacks.OnOpenSigXorUpdate += () => Dispatcher.BeginInvoke(new Action(() => _sigXor?.OpenUpdateWindow()));
    }

    private static void RestartProcess(bool asAdmin)
    {
        var processPath = Environment.ProcessPath;
        string fileName;
        string arguments;
        // `dotnet run`: ProcessPath is dotnet.exe → relaunch the app assembly under BaseDirectory.
        // Published single-file: ProcessPath is DevEnv.exe（Assembly.Location 在单文件下为空）。
        if (processPath is not null
            && Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var dll = Path.Combine(AppContext.BaseDirectory, "DevEnv.dll");
            if (!File.Exists(dll))
                return;
            fileName = processPath;
            arguments = $"\"{dll}\"";
        }
        else
        {
            fileName = processPath ?? string.Empty;
            arguments = string.Empty;
        }

        if (string.IsNullOrEmpty(fileName))
            return;

        try
        {
            if (asAdmin)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Environment.CurrentDirectory,
                });
            }
            else
            {
                // explorer 降权重启：去掉管理员令牌
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = string.IsNullOrEmpty(arguments) ? $"\"{fileName}\"" : $"\"{fileName}\" {arguments}",
                    UseShellExecute = true,
                });
            }

            if (System.Windows.Application.Current is App running)
                running.ExitApp();
        }
        catch (Win32Exception)
        {
            // UAC 已取消
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"无法重启: {ex.Message}", "FireNeb DevEnv", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    #endregion

    #region 剪贴板历史

    private static void OnClipboardCaptured(ClipboardCapture capture)
    {
        try
        {
            if (capture.Kind == "text")
                _ = CoreServices.Clipboard.CaptureText(capture.Text ?? string.Empty);
            else if (capture.PngBytes is not null)
                _ = CoreServices.Clipboard.CaptureImage(capture.PngBytes);
        }
        catch
        {
            // 写库失败不影响主流程
        }
    }

    #endregion
}