using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.Views;
using DevEnv.Host.Windows;

namespace DevEnv.Host.ViewModels;

/// <summary>主窗口视图模型：左侧导航 + 当前页面 + 状态栏 + 顶部菜单命令。</summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>程序化同步导航高亮时置位，避免 OnCurrentNavChanged 再导航一次、把同一页面创建两份。</summary>
    private bool _syncingNav;

    public MainViewModel(IReadOnlyList<NavItem> navItems)
    {
        NavItems = new ObservableCollection<NavItem>(navItems);
        StatusText = "就绪";
        VersionText = $"版本 {CurrentVersion()}";
        CurrentNav = NavItems.FirstOrDefault();
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
            SettingsDialogHost.Open();
            return;
        }

        // 本地应用 / 文件夹 / 网站 / 系统工具已移到独立的启动器窗口，
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

    /// <summary>是否为全局启动器的页签 id（apps / folders / webs）。</summary>
    private static bool IsShortcutPage(string id) =>
        id.Equals("apps", StringComparison.OrdinalIgnoreCase)
        || id.Equals("folders", StringComparison.OrdinalIgnoreCase)
        || id.Equals("webs", StringComparison.OrdinalIgnoreCase)
        || id.Equals("startmenu", StringComparison.OrdinalIgnoreCase)
        || id.Equals("tools", StringComparison.OrdinalIgnoreCase)
        || id.Equals("builtin", StringComparison.OrdinalIgnoreCase);

    private static string CurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}