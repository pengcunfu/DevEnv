using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Core.Models;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Views;

/// <summary>进程监控页。离开页面时停掉轮询定时器；双击行打开详情窗口。</summary>
public partial class ProcessesView : UserControl
{
    public ProcessesView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }

    /// <summary>用 PreviewMouseLeftButtonDown 判双击：DataGridCell 会把普通双击事件吃掉。</summary>
    private void OnGridPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
            return;
        if (VisualTreeSearch.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is not ProcessRowView row)
            return;

        e.Handled = true;
        ProcessDetailWindow.Show(HostServices.DialogOwner, row);
    }
}
