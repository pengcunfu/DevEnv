using System.Windows;
using DevEnv.Core;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.Windows;

/// <summary>
/// 设置对话框宿主：全局只保留一个设置窗口实例，多入口（主窗口菜单 / SigXor 入口）共用。
///
/// 设置窗口是平铺的四页签，不做二级页签：
/// - 「常规」：主程序设置（托盘、自启、主题、默认翻译引擎）+ 模块的常规选项；
/// - 「快捷键」：所有程序绑定的快捷键；
/// - 「语音与截屏」：语音输入与区域截屏；
/// - 「服务与模型」：语音 / OCR 模型与后台服务。
/// 「关于与帮助」已独立为 AboutHelpWindow，从主窗口菜单打开。
/// 中间三页签由模块通过 <see cref="ISettingsSections"/> 提供内容，其余由主程序提供。
/// </summary>
public static class SettingsDialogHost
{
    /// <summary>页签索引：0 常规 / 1 快捷键 / 2 语音与截屏 / 3 服务与模型。</summary>
    public const int GeneralTabIndex = 0;
    public const int HotkeysTabIndex = 1;
    public const int VoiceTabIndex = 2;
    public const int ServiceTabIndex = 3;

    private static SettingsWindow? _window;

    /// <summary>模块提供的分区内容（由 App 在模块初始化后注入）。</summary>
    public static ISettingsSections? Sections { get; set; }

    /// <summary>打开设置窗口；已打开时仅切到目标页签并激活。</summary>
    public static void Open(int tabIndex = GeneralTabIndex)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        // 已经开着（模态对话框在 ShowDialog 期间仍可从宿主回调再进来）：只切页签
        if (_window is not null && _window.IsVisible)
        {
            _window.SelectTab(tabIndex);
            _window.Activate();
            return;
        }

        var window = new SettingsWindow();
        if (Sections is { } sections)
        {
            window.SetGeneralContent(sections.General);
            window.SetHotkeysContent(sections.Hotkeys);
            window.SetVoiceContent(sections.Voice);
            window.SetServiceContent(sections.Service);
        }
        window.Closed += (_, _) => _window = null;
        _window = window;

        // 设置窗口与主窗口保持同样的大小和位置：主窗口可见时直接复制它的几何，
        // 主窗口隐藏（例如从托盘 / 模块回调进入）时摆到启动器的默认几何上。
        if (app.MainWindow is { IsVisible: true } owner && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
            ApplyMainWindowBounds(window, owner);
        }
        else
        {
            var bounds = CursorScreen.GetLauncherBoundsDip();
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Width = bounds.Width;
            window.Height = bounds.Height;
            window.Left = bounds.Left;
            window.Top = bounds.Top;
        }

        window.SelectTab(tabIndex);
        window.ShowDialog();
    }

    /// <summary>
    /// 把设置窗口摆到与主窗口相同的尺寸和位置。主窗口最大化 / 最小化时用还原后的几何。
    /// 供自检探针直接验证。
    /// </summary>
    internal static bool ApplyMainWindowBounds(Window window, Window? main)
    {
        if (main is null || ReferenceEquals(main, window))
            return false;

        var bounds = main.WindowState == WindowState.Normal
            ? new Rect(main.Left, main.Top, main.Width, main.Height)
            : main.RestoreBounds;
        if (bounds.IsEmpty
            || double.IsNaN(bounds.Left) || double.IsNaN(bounds.Top)
            || double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height)
            || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        window.Left = bounds.Left;
        window.Top = bounds.Top;
        return true;
    }

    /// <summary>关闭设置窗口（应用退出时调用）。</summary>
    public static void Close()
    {
        try { _window?.Close(); } catch { }
        _window = null;
    }
}
