using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

/// <summary>文件夹预览里的文件管理器视图。</summary>
public partial class FolderView : UserControl
{
    public FolderView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (IsLoaded)
                FileList.Focus();
        };
    }

    private FolderContentViewModel? Vm => DataContext as FolderContentViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e) => FileList.Focus();

    private void OnFileDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 双击列表空白处不触发当前选中项
        if (FindAncestor<ListViewItem>(e.OriginalSource as DependencyObject) is null)
            return;

        if (Vm is { } vm)
            _ = vm.ActivateSelectedAsync();
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                if (Vm is { } enterVm)
                    _ = enterVm.ActivateSelectedAsync();
                break;
            case Key.Back:
                e.Handled = true;
                if (Vm?.NavigateUpCommand.CanExecute(null) == true)
                    Vm.NavigateUpCommand.Execute(null);
                break;
        }
    }

    private void OnPathKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        Go();
    }

    private void OnGoClick(object sender, RoutedEventArgs e) => Go();

    private void Go()
    {
        if (Vm is not { } vm || string.IsNullOrWhiteSpace(vm.PathInput))
            return;

        _ = vm.NavigateToAsync(vm.PathInput);
    }

    private void OnOpenExplorerClick(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || string.IsNullOrWhiteSpace(vm.CurrentDirectory))
            return;

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", vm.CurrentDirectory)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // 资源管理器不可用时保持预览窗口不变
        }
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
                return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }
}
