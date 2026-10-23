using System.Windows;
using System.Windows.Controls;
using DevEnv.Core;

namespace DevEnv.Host.Views;

/// <summary>主壳里的剪贴板入口页：历史本体在 ClipboardWindow（纯 WPF 独立窗）。</summary>
public partial class ClipboardEntryView : UserControl
{
    public ClipboardEntryView() => InitializeComponent();

    private void OnOpenClick(object sender, RoutedEventArgs e) =>
        HostCallbacks.OnShowClipboard?.Invoke(true);
}