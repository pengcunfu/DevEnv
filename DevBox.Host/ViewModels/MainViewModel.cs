using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBox.Core;
using DevBox.Core.Models;
using DevBox.Core.Services;
using DevBox.Host.Infrastructure;
using DevBox.Host.Views;

namespace DevBox.Host.ViewModels;

/// <summary>主窗口视图模型：左侧导航 + 当前页面 + 状态栏 + 顶部菜单命令。</summary>
public sealed partial class MainViewModel : ObservableObject
{
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
    private string _currentPageId = "apps";

    [ObservableProperty]
    private UserControl? _currentView;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _versionText = string.Empty;

    /// <summary>给状态栏写一句话（拖放添加、工具启动结果等）。</summary>
    public void SetStatus(string message) => StatusText = message;

    public string PageTitle => CurrentPageId switch
    {
        "translator" => "熔岩翻译",
        "clipboard" => "熔岩剪贴板",
        "settings" => "设置",
        var id => NavItems.FirstOrDefault(n => n.Id == id)?.Title ?? id,
    };

    partial void OnCurrentNavChanged(NavItem? value)
    {
        if (value is not null)
            Navigate(value.Id);
    }

    /// <summary>导航到指定页面（菜单、系统工具回调、二次启动唤醒都走这里）。</summary>
    public void Navigate(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var normalized = id.TrimStart('/');
        // 从左侧导航进入时同步高亮；工具子页不属于导航项，保持现状即可
        var nav = NavItems.FirstOrDefault(n => n.Id == normalized);
        if (nav is not null && !ReferenceEquals(nav, CurrentNav))
            CurrentNav = nav;

        CurrentPageId = normalized;
        CurrentView = ViewFactory.Create(normalized);
    }

    /// <summary>工具一律开独立非模态窗口，不再嵌入主窗内容区。</summary>
    [RelayCommand]
    private void OpenTool(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        var window = System.Windows.Application.Current?.MainWindow as MainWindow;
        window?.Show();
        ToolWindowHost.Open(id);
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
    private void OpenDocs() => HostServices.OpenUrl("https://docs.devenv.cn/");

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

    private static string CurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}