using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Core.Services;

namespace DevEnv.Host.Windows;

/// <summary>熔岩剪贴板独立窗（纯 WPF）：Ctrl+Space 唤起，关闭只隐藏。</summary>
public partial class ClipboardWindow : Window
{
    private bool _allowClose;

    public ClipboardWindow()
    {
        InitializeComponent();
        foreach (var (_, label) in ((ViewModels.ClipboardViewModel)DataContext).ClearModes)
            ClearModeCombo.Items.Add(label);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
            return;
        e.Cancel = true;
        Hide();
    }

    public void PrepareForExit() => _allowClose = true;

    public void ShowAndActivate()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        ((ViewModels.ClipboardViewModel)DataContext).Reload();
    }

    private void OnClearModeChanged(object sender, SelectionChangedEventArgs e)
    {
        var combo = ClearModeCombo;
        if (combo.SelectedIndex < 0)
            return;

        var label = combo.SelectedItem as string ?? string.Empty;
        var mode = ((ViewModels.ClipboardViewModel)DataContext).ClearModes
            .FirstOrDefault(m => m.Label == label).Mode ?? "all";
        // 取消选中，回到「清空记录…」占位提示，方便重复操作。
        combo.SelectedIndex = -1;
        ((ViewModels.ClipboardViewModel)DataContext).Clear(mode);
    }

    private void OnEntryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: not null })
            ((ViewModels.ClipboardViewModel)DataContext).Paste(null);
    }
}