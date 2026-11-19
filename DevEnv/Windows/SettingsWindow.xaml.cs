using System.Windows;
using System.Windows.Input;
using DevEnv.Core.Input;
using DevEnv.Host.Views;

namespace DevEnv.Host.Windows;

/// <summary>
/// 设置对话框：平铺的四页签（常规 / 快捷键 / 语音与截屏 / 服务与模型），不做二级页签。
/// 「关于与帮助」已独立为 AboutHelpWindow，从主窗口菜单打开。
///
/// 「常规」页由主程序设置（<c>SettingsView</c>）与模块注入的常规选项组成；
/// 「快捷键」「语音与截屏」「服务与模型」三页的内容由 SigXor 模块在运行时注入。
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += OnClosed;
    }

    /// <summary>切换到指定页签（0 常规 / 1 快捷键 / 2 语音与截屏 / 3 服务与模型）。</summary>
    public void SelectTab(int index)
    {
        Tabs.SelectedIndex = index >= 0 && index < Tabs.Items.Count ? index : 0;
    }

    /// <summary>注入「常规」页里追加的模块常规选项。</summary>
    public void SetGeneralContent(object? content) => SigXorGeneralSlot.Content = content;

    /// <summary>注入「快捷键」页的内容。</summary>
    public void SetHotkeysContent(object? content) => HotkeysSlot.Content = content;

    /// <summary>注入「语音与截屏」页的内容。</summary>
    public void SetVoiceContent(object? content) => VoiceSlot.Content = content;

    /// <summary>注入「服务与模型」页的内容。</summary>
    public void SetServiceContent(object? content) => ServiceSlot.Content = content;

    private void OnClosed(object? sender, EventArgs e)
    {
        // 关闭时结束可能仍在进行的快捷键录制，恢复全局热键
        CancelHotkeyCapture();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        // 录制中 Esc 只取消录制，不关闭设置窗口
        if (HotkeyCaptureState.IsCapturing)
        {
            CancelHotkeyCapture();
            e.Handled = true;
            return;
        }

        Close();
        e.Handled = true;
    }

    private void CancelHotkeyCapture()
    {
        if (HotkeysSlot.Content is HotkeysSettingsView view)
            view.CancelCapture();
    }
}
