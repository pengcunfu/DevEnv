using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Core.Models;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.ViewModels;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Views;

/// <summary>网络监控：按进程展示流量；离开页面时停掉定时器。</summary>
public partial class NetworkView : UserControl
{
    public NetworkView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }

    /// <summary>用 PreviewMouseLeftButtonDown 判双击：DataGridCell 会把普通双击事件吃掉。</summary>
    private void OnProcessGridPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
            return;
        if (VisualTreeSearch.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is not NetworkProcessView process)
            return;

        e.Handled = true;
        NetworkConnectionWindow.Show(HostServices.DialogOwner, process);
    }
}
