using System.Windows;
using System.Windows.Input;

namespace DevEnv.Host.Windows;

/// <summary>
/// 设置对话框：平铺的三页签（常规 / 语音与截屏 / 服务与模型），不做二级页签。
///
/// 「常规」页由主程序设置（<c>SettingsView</c>）与模块注入的常规选项组成；
/// 「语音与截屏」「服务与模型」两页的内容由 SigXor 模块在运行时注入。
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += OnClosed;
    }

    /// <summary>切换到指定页签（0 常规 / 1 语音与截屏 / 2 服务与模型）。</summary>
    public void SelectTab(int index)
    {
        Tabs.SelectedIndex = index >= 0 && index < Tabs.Items.Count ? index : 0;
    }

    /// <summary>注入「常规」页里追加的模块常规选项。</summary>
    public void SetGeneralContent(object? content) => SigXorGeneralSlot.Content = content;

    /// <summary>注入「语音与截屏」页的内容。</summary>
    public void SetVoiceContent(object? content) => VoiceTab.Content = content;

    /// <summary>注入「服务与模型」页的内容。</summary>
    public void SetServiceContent(object? content) => ServiceTab.Content = content;

    private void OnClosed(object? sender, EventArgs e)
    {
        // 常规页的下载进度轮询随窗口一起释放，不在 Unloaded 时停（切页签会触发 Unloaded）
        if (GeneralHostView is not null)
            GeneralHostView.Release();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        Close();
        e.Handled = true;
    }
}
