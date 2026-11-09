using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Windows;

/// <summary>熔岩剪贴板独立窗（纯 WPF）：从 Ctrl+Space 热键或托盘菜单打开，关闭只隐藏。</summary>
public partial class ClipboardWindow : Window
{
    private bool _allowClose;

    public ClipboardWindow()
    {
        InitializeComponent();
    }

    private ClipboardViewModel ViewModel => (ClipboardViewModel)DataContext;

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
        ViewModel.Reload();
    }

    /// <summary>左列表右键时先选中指针下的条目，再弹菜单。</summary>
    private void OnEntryListRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (VisualTreeSearch.FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is { } item)
            item.IsSelected = true;
    }

    private void OnEntryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: not null })
            ViewModel.Paste(null);
    }

    /// <summary>没有选中记录时，右键菜单里的操作置灰。</summary>
    private void OnEntryMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        var enabled = ViewModel.Selected is not null;
        foreach (var item in menu.Items.OfType<MenuItem>())
            item.IsEnabled = enabled;
    }

    private void OnEntryCopyClick(object sender, RoutedEventArgs e) => ViewModel.Paste(null);

    private void OnEntryDeleteClick(object sender, RoutedEventArgs e) =>
        ViewModel.DeleteCommand.Execute(ViewModel.Selected);

    private void OnClearButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
            return;

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    /// <summary>菜单项 Tag 形如 "text|today"、"image|all"，第一段是类型，第二段是时间范围。</summary>
    private void OnClearRangeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag })
            return;

        var parts = tag.Split('|');
        if (parts.Length != 2)
            return;

        var type = parts[0] switch
        {
            "text" => "text",
            "image" => "image",
            _ => null,
        };
        ViewModel.Clear(parts[1], type);
    }
}
