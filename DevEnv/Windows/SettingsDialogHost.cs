using System.Windows;
using DevEnv.Core;

namespace DevEnv.Host.Windows;

/// <summary>
/// 设置对话框宿主：全局只保留一个设置窗口实例，多入口（主菜单 / 托盘 / SigXor 入口）共用。
///
/// 设置窗口是平铺的三页签，不做二级页签：
/// - 「常规」：主程序设置（托盘、自启、主题、热键、默认翻译引擎、检查更新）+ 模块的常规选项；
/// - 「语音与截屏」：语音输入与区域截屏；
/// - 「服务与模型」：语音 / OCR 模型与后台服务。
/// 后两页签由模块通过 <see cref="ISettingsSections"/> 提供内容。
/// </summary>
public static class SettingsDialogHost
{
    /// <summary>页签索引：0 常规 / 1 语音与截屏 / 2 服务与模型。</summary>
    public const int GeneralTabIndex = 0;
    public const int VoiceTabIndex = 1;
    public const int ServiceTabIndex = 2;

    private static SettingsWindow? _window;

    /// <summary>模块提供的分区内容（由 App 在模块初始化后注入）。</summary>
    public static ISettingsSections? Sections { get; set; }

    /// <summary>设置对话框是否正开着（模态期间 Win 键不弹启动台）。</summary>
    public static bool IsOpen => _window is { IsVisible: true };

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
            window.SetVoiceContent(sections.Voice);
            window.SetServiceContent(sections.Service);
        }
        window.Closed += (_, _) => _window = null;
        _window = window;

        if (app.MainWindow is { } owner && owner.IsVisible && !ReferenceEquals(owner, window))
            window.Owner = owner;

        window.SelectTab(tabIndex);
        window.ShowDialog();
    }

    /// <summary>关闭设置窗口（应用退出时调用）。</summary>
    public static void Close()
    {
        try { _window?.Close(); } catch { }
        _window = null;
    }
}
