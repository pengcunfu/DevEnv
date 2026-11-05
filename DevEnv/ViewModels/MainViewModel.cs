using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.Views;
using DevEnv.Host.Windows;

namespace DevEnv.Host.ViewModels;

/// <summary>主窗口视图模型：左侧导航 + 当前页面 + 状态栏 + 顶部菜单命令。</summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>程序化同步导航高亮时置位，避免 OnCurrentNavChanged 再导航一次、把同一页面创建两份。</summary>
    private bool _syncingNav;

    public MainViewModel()
    {
        NavItems = new ObservableCollection<NavItem>(ViewFactory.NavItems);
        StatusText = "就绪";
        VersionText = $"版本 {CurrentVersion()}";
        CurrentNav = NavItems.FirstOrDefault();

        // 调试用：DEVENV_START_PAGE=lava 直接启动到指定页，便于自动化截图与手工排查
        var startPage = Environment.GetEnvironmentVariable("DEVENV_START_PAGE");
        if (!string.IsNullOrWhiteSpace(startPage))
            Navigate(startPage);
    }

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    private NavItem? _currentNav;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private string _currentPageId = "devenv";

    [ObservableProperty]
    private UserControl? _currentView;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>页面自己的摘要（例如网络监控的连接数 / 速率），显示在状态栏左半部分。</summary>
    [ObservableProperty]
    private string _pageStatusText = string.Empty;

    [ObservableProperty]
    private string _versionText = string.Empty;

    /// <summary>给状态栏写一句话（拖放添加、工具启动结果等）。</summary>
    public void SetStatus(string message) => StatusText = message;

    /// <summary>页面级摘要，切页时由页面自行写入与清空。</summary>
    public void SetPageStatus(string message) => PageStatusText = message;

    public string PageTitle => CurrentPageId switch
    {
        "translator" => "熔岩翻译",
        "clipboard" => "熔岩剪贴板",
        "settings" => "设置",
        var id => NavItems.FirstOrDefault(n => n.Id == id)?.Title ?? id,
    };

    partial void OnCurrentNavChanged(NavItem? value)
    {
        if (!_syncingNav && value is not null)
            Navigate(value.Id);
    }

    /// <summary>导航到指定页面（菜单、系统工具回调、二次启动唤醒都走这里）。</summary>
    public void Navigate(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var normalized = id.TrimStart('/');
        // 「设置」不再是内容页，任何入口都统一弹设置对话框。
        if (normalized.Equals("settings", StringComparison.OrdinalIgnoreCase))
        {
            OpenSettings();
            return;
        }

        // 本地应用 / 文件夹 / 网站已移到独立的启动器窗口，
        // 拖放、DEVENV_START_PAGE 等入口统一改为打开该窗口的对应页签。
        // 延后到空闲时打开：启动阶段（DEVENV_START_PAGE）主窗还没 Show，
        // 立刻弹出的全屏启动器会被主窗自己的激活操作挤掉。
        if (IsShortcutPage(normalized))
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null)
            {
                dispatcher.BeginInvoke(
                    new Action(() => ShortcutWindowHost.Open(normalized)),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                ShortcutWindowHost.Open(normalized);
            }
            return;
        }

        // 从左侧导航进入时同步高亮；工具子页不属于导航项，保持现状即可
        var nav = NavItems.FirstOrDefault(n => n.Id == normalized);
        if (nav is not null && !ReferenceEquals(nav, CurrentNav))
        {
            _syncingNav = true;
            CurrentNav = nav;
            _syncingNav = false;
        }

        CurrentPageId = normalized;
        PageStatusText = string.Empty;
        CurrentView = ViewFactory.Create(normalized);
    }

    /// <summary>设置以模态对话框打开；已打开时仅激活，避免重复弹窗。</summary>
    [RelayCommand]
    private void OpenSettings() => SettingsDialogHost.Open();

    /// <summary>快捷方式窗口：本地应用 / 文件夹 / 网站三个横向页签。</summary>
    [RelayCommand]
    private void OpenShortcuts() => ShortcutWindowHost.Open();

    /// <summary>工具一律开独立非模态窗口，不再嵌入主窗内容区。</summary>
    [RelayCommand]
    private void OpenTool(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        var window = System.Windows.Application.Current?.MainWindow as MainWindow;
        window?.Show();
        var result = ToolWindowHost.Open(id);
        if (!result.Ok)
            StatusText = result.DisplayMessage;
    }

    [RelayCommand]
    private void ExitApp() => HostCallbacks.OnExitApp?.Invoke();

    [RelayCommand]
    private void ShowTranslator()
    {
        var siteId = CoreServices.Translator.GetLastSiteId();
        TranslatorService.ShowTranslator(new TranslatorRequest { Visible = true, SiteId = siteId });
    }

    [RelayCommand]
    private void HideTranslator() => TranslatorService.HideTranslator();

    [RelayCommand]
    private void ShowClipboard() => HostCallbacks.OnShowClipboard?.Invoke(true);

    [RelayCommand]
    private void About() => HostServices.ShowAbout();

    [RelayCommand]
    private void OpenDocs() => HostServices.OpenUrl("https://devenv.cn/");

    // SigXor 模块：文件快速预览 / 区域截图 / 语音与截屏 / 全局启动器 / 文件浏览器

    /// <summary>SigXor 模块：文件快速预览（选择文件后弹预览浮窗）。</summary>
    [RelayCommand]
    private void OpenFilePreview() => HostCallbacks.OnOpenFilePreview?.Invoke();

    /// <summary>SigXor 模块：区域截图 / OCR / 取色。</summary>
    [RelayCommand]
    private void OpenScreenshot() => HostCallbacks.OnOpenScreenshot?.Invoke();

    /// <summary>SigXor 模块：语音与截屏设置。</summary>
    [RelayCommand]
    private void OpenSigXorSettings() => HostCallbacks.OnOpenSigXorSettings?.Invoke(SettingsSection.Voice);

    /// <summary>SigXor 模块：服务与模型设置。</summary>
    [RelayCommand]
    private void OpenSigXorServiceSettings() => HostCallbacks.OnOpenSigXorSettings?.Invoke(SettingsSection.Service);

    /// <summary>SigXor 模块：全局启动器（应用 / 文件 / 命令启动）。</summary>
    [RelayCommand]
    private void OpenSigXorLauncher() => HostCallbacks.OnOpenSigXorLauncher?.Invoke();

    /// <summary>SigXor 模块：文件浏览器（目录导航 + 内嵌预览）。</summary>
    [RelayCommand]
    private void OpenSigXorBrowser() => HostCallbacks.OnOpenSigXorBrowser?.Invoke();

    /// <summary>SigXor 模块：关于窗口。</summary>
    [RelayCommand]
    private void OpenSigXorAbout() => HostCallbacks.OnOpenSigXorAbout?.Invoke();

    /// <summary>工具菜单项：启动对应 Windows 工具或跳到内置工具页。</summary>
    [RelayCommand]
    private void RunSystemTool(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;
        var result = CoreServices.Shortcuts.RunSystemTool(key);
        if (!result.Ok)
            StatusText = result.DisplayMessage;
    }

    /// <summary>是否为快捷方式窗口的页签 id（apps / folders / webs）。</summary>
    private static bool IsShortcutPage(string id) =>
        id.Equals("apps", StringComparison.OrdinalIgnoreCase)
        || id.Equals("folders", StringComparison.OrdinalIgnoreCase)
        || id.Equals("webs", StringComparison.OrdinalIgnoreCase);

    private static string CurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}