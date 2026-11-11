using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using DevEnv.Core;
using DevEnv.Core.SigXor;
using DevEnv.Host.SigXor.Services;
using DevEnv.Host.SigXor.Toolkit.Capture;
using DevEnv.Host.SigXor.Toolkit.Voice;
using DevEnv.Host.SigXor.ViewModels;
using DevEnv.Host.SigXor.Views;
using DevEnv.Host.Views;
using HostWindows = DevEnv.Host.Windows;

namespace DevEnv.Host.SigXor;

/// <summary>
/// SigXor 模块装配入口。
///
/// 原 SigXor 独立应用的 <c>App</c> 已改造为此类：
/// - 不再继承 <see cref="Application"/>、不再自带托盘 / 单实例 / 独立入口
///   （这些统一由 <c>DevEnv.Host.App</c> 与 <c>TrayIconHost</c> 提供）；
/// - 只保留模块自己的接线与生命周期，把窗口与服务的创建交给宿主调用。
///
/// 生命周期：宿主在 <c>App.OnStartup</c> 调 <see cref="Initialize"/>，
/// 在退出时调 <see cref="Dispose"/>。
/// </summary>
public sealed class SigXorHost : IDisposable
{
    private readonly SettingsService _settings = new();
    private readonly BackupService _backup;
    private readonly Dispatcher _dispatcher;

    private GlobalHotkeyRouter? _hotkeyRouter;
    private GlobalHotkeyService? _hotkeys;
    private WpfScreenService? _screens;
    private ShellPreviewService? _shellPreview;
    private VoiceInputService? _voiceService;
    private ScreenshotService? _screenshotService;

    private SigXorGeneralView? _generalView;
    private HotkeysSettingsView? _hotkeysView;
    private SigXorVoiceView? _voiceView;
    private SigXorServiceView? _serviceView;
    private ISettingsSections? _sections;
    private UpdateWindow? _updateWindow;
    private MainWindow? _browserWindow;
    private bool _disposed;

    public SigXorHost(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _backup = new BackupService(_settings);
    }

    /// <summary>模块已初始化完成（热键钩子、后台服务就位）。</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>语音服务是否正在运行。</summary>
    public bool IsVoiceServiceRunning => _voiceService?.IsRunning == true;

    /// <summary>模块产生的通知（气泡提示），由宿主转发到托盘。</summary>
    public event Action<string, string>? NotificationRequested;

    /// <summary>语音服务状态变化，由宿主转发到托盘菜单。</summary>
    public event Action<VoiceServiceStatus>? VoiceStatusChanged;

    /// <summary>
    /// 初始化模块：迁移旧配置、注册语法高亮、装配热键路由与三个后台服务。
    /// </summary>
    /// <param name="silent">静默启动（开机自启时不弹气泡、不检查更新）。</param>
    public void Initialize(bool silent = false)
    {
        if (IsInitialized) return;

        AppPaths.EnsureCreated();
        if (SigXorSettingsMigrator.TryMigrate(_settings.Current))
            _settings.Save();

        // 注册自定义语法高亮定义（JSON、TOML、YAML、Log）
        SyntaxHighlightingService.Initialize();

        // 全局键盘钩子：资源管理器里按空格预览。
        // 钩子必须先于各服务创建，三个服务都注册到同一个路由上：
        //   截图 300 > 语音 200 > 预览 100（优先级高的先拿到按键）
        _hotkeyRouter = new GlobalHotkeyRouter();
        _hotkeys = new GlobalHotkeyService
        {
            KeyDown = _hotkeyRouter.HandleKeyDown,
            KeyUp = _hotkeyRouter.HandleKeyUp,
        };
        _screens = new WpfScreenService();

        _shellPreview = new ShellPreviewService(_settings, _backup, _dispatcher, _hotkeyRouter);
        _voiceService = new VoiceInputService(_settings, _hotkeyRouter, _screens, _dispatcher);
        _voiceService.NotificationRequested += (title, message) => NotificationRequested?.Invoke(title, message);
        _voiceService.StatusChanged += (_, status) => VoiceStatusChanged?.Invoke(status);

        _screenshotService = new ScreenshotService(_settings, _hotkeyRouter, _screens, _dispatcher);
        _screenshotService.NotificationRequested += (title, message) => NotificationRequested?.Invoke(title, message);

        _hotkeys.Install();

        // 后台预热 WebView2 环境与各预览宿主，缩短 md / pdf / office 首次预览等待
        WebViewHostPool.StartWarmup(_dispatcher);

        if (_settings.Current.Voice.AutoStartService)
            _ = _voiceService.TryAutoStartAsync();

        if (!silent)
            CheckForUpdatesOnStartup();

        IsInitialized = true;
    }

    /// <summary>打开预览浮窗预览给定文件（多文件时可用 ↑/↓ 切换）。</summary>
    public void ShowPreviewForPaths(IEnumerable<string> paths)
    {
        if (!IsInitialized) Initialize();
        _shellPreview?.ShowPreviewForPaths(paths);
    }

    /// <summary>打开文件浏览器窗口（目录导航 + 内嵌预览）。</summary>
    public void ShowBrowser()
    {
        if (!IsInitialized) Initialize();
        if (_browserWindow is not null)
        {
            try
            {
                Host.Infrastructure.CursorScreen.CenterIfHidden(_browserWindow);
                _browserWindow.Show();
                _browserWindow.Activate();
                _browserWindow.WindowState = WindowState.Normal;
                return;
            }
            catch (ArgumentException)
            {
                try { _browserWindow.Close(); } catch { }
                _browserWindow = null;
            }
        }

        var preview = new PreviewViewModel(_settings, _backup);
        var vm = new MainViewModel(_settings, preview);
        _browserWindow = new MainWindow();
        _browserWindow.Initialize(vm, _settings);
        _browserWindow.Closed += (_, _) => _browserWindow = null;
        Host.Infrastructure.CursorScreen.Center(_browserWindow);
        _browserWindow.Show();
        _browserWindow.Activate();
        _ = vm.InitializeAsync();
    }

    /// <summary>开始区域截图（拖动选区 → 工具条 → OCR / 取色 / 保存）。</summary>
    public void StartCapture() => _screenshotService?.StartCapture();

    /// <summary>启动语音服务。</summary>
    public void StartVoiceService() => _ = _voiceService?.StartServiceAsync();

    /// <summary>停止语音服务。</summary>
    public void StopVoiceService() => _voiceService?.StopService();

    /// <summary>
    /// 打开设置：内容已并入FireNeb DevEnv的设置对话框，这里只负责跳转到对应页签。
    /// </summary>
    /// <param name="section">目标页签（常规 / 快捷键 / 语音与截屏 / 服务与模型）。</param>
    public void OpenSettings(SettingsSection section = SettingsSection.General)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => OpenSettings(section));
            return;
        }

        // 先确保分区内容已就绪，宿主取内容时才有东西可注入
        EnsureSettingsSections();

        int index = section switch
        {
            SettingsSection.Hotkeys => HostWindows.SettingsDialogHost.HotkeysTabIndex,
            SettingsSection.Voice => HostWindows.SettingsDialogHost.VoiceTabIndex,
            SettingsSection.Service => HostWindows.SettingsDialogHost.ServiceTabIndex,
            _ => HostWindows.SettingsDialogHost.GeneralTabIndex,
        };
        HostWindows.SettingsDialogHost.Open(index);
    }

    /// <summary>创建设置对话框需要的四个分区内容（供宿主注入）。</summary>
    public ISettingsSections CreateSettingsSections()
    {
        if (!_dispatcher.CheckAccess())
            return _dispatcher.Invoke(CreateSettingsSections);

        EnsureSettingsSections();
        return _sections!;
    }

    /// <summary>构建（并缓存）四个分区视图。</summary>
    private void EnsureSettingsSections()
    {
        if (_sections is not null) return;

        _generalView ??= new SigXorGeneralView(_settings);
        _hotkeysView ??= new HotkeysSettingsView(_settings, OnShortcutsSaved);
        _voiceView ??= new SigXorVoiceView(_settings);
        _serviceView ??= new SigXorServiceView(_voiceService);

        RefreshShortcutHint();

        _sections = new SettingsSections(_generalView, _hotkeysView, _voiceView, _serviceView);
    }

    /// <summary>「快捷键」页保存后，刷新「服务与模型」页里的使用说明。</summary>
    private void OnShortcutsSaved() => RefreshShortcutHint();

    private void RefreshShortcutHint()
    {
        _serviceView?.SetShortcutHint(_hotkeysView?.ShortcutHint ?? _voiceView?.ShortcutHint);
    }

    /// <summary>打开更新窗口（单实例复用，关闭后重建）。</summary>
    public void OpenUpdateWindow()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(OpenUpdateWindow);
            return;
        }

        if (_updateWindow is not null)
        {
            try
            {
                _updateWindow.Show();
                _updateWindow.Activate();
                _updateWindow.WindowState = WindowState.Normal;
                return;
            }
            catch (ArgumentException)
            {
                try { _updateWindow.Close(); } catch { }
                _updateWindow = null;
            }
        }

        var updater = new UpdateService();
        _updateWindow = new UpdateWindow(new UpdateViewModel(updater));
        _updateWindow.Closed += (_, _) => _updateWindow = null;
        Host.Infrastructure.CursorScreen.Center(_updateWindow);
        _updateWindow.Show();
        _updateWindow.Activate();
    }

    /// <summary>启动时后台静默检查更新（受设置开关控制）；发现新版本用气泡轻提示。</summary>
    private async void CheckForUpdatesOnStartup()
    {
        try
        {
            if (!_settings.Current.CheckUpdatesOnStartup) return;

            var updater = new UpdateService();
            if (!updater.IsUpdateCapable) return; // 调试构建 / 非安装态：跳过

            bool hasUpdate = await updater.CheckForUpdatesAsync();
            if (!hasUpdate) return; // 已是最新

            NotificationRequested?.Invoke("FireNeb DevEnv 有可用更新",
                $"新版本 v{updater.PendingVersion} 已发布（当前 v{updater.CurrentVersion}），点击此提示可查看。");
        }
        catch
        {
            // 离线 / GitHub 限流 / 网络异常：静默失败，不打扰用户
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _generalView = null; _voiceView = null; _serviceView = null; _sections = null; } catch { }
        try { _updateWindow?.Close(); } catch { }
        try { _browserWindow?.Close(); } catch { }
        try { _voiceService?.Dispose(); } catch { }
        try { _screenshotService?.Dispose(); } catch { }
        try { _shellPreview?.Dispose(); } catch { }
        try { WebViewHostPool.DisposeAll(); } catch { }
        try { _hotkeys?.Dispose(); } catch { }
    }
}
