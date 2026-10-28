using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Core.Models;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.ViewModels;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Views;

/// <summary>网络监控页。页签切换决定哪个定时器在刷新；离开页面时全部停掉。</summary>
public partial class NetworkView : UserControl
{
    public NetworkView()
    {
        InitializeComponent();
        Tabs.SelectionChanged += OnTabsSelectionChanged;
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }

    private NetworkViewModel ViewModel => (NetworkViewModel)DataContext;

    /// <summary>
    /// SelectionChanged 会从内部 DataGrid 冒泡上来，只处理 TabControl 自己发的事件。
    /// 否则点一行进程就会触发切页签逻辑，重刷列表又改选中项，来回递归直接栈溢出。
    /// </summary>
    private void OnTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, Tabs))
            return;

        ViewModel.OnTabChanged(Tabs.SelectedIndex);
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
