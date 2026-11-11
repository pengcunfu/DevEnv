using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Core.ServiceWatch.Models;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.ViewModels;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Views;

/// <summary>服务监控页。详情进模态框，摘要进工具窗状态条；离开页面时停掉轮询。</summary>
public partial class ServicesView : UserControl
{
    public ServicesView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as System.IDisposable)?.Dispose();
    }

    private ServicesViewModel ViewModel => (ServicesViewModel)DataContext;

    private void OnShowDetailClick(object sender, RoutedEventArgs e) => ShowDetail();

    /// <summary>用 PreviewMouseLeftButtonDown 判双击：DataGridCell 会把普通双击事件吃掉。</summary>
    private void OnServiceGridPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
            return;
        if (VisualTreeSearch.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is not ServiceInfo service)
            return;

        e.Handled = true;
        ViewModel.Selected = service;
        ShowDetail();
    }

    private void ShowDetail()
    {
        if (!ViewModel.HasSelection)
            return;
        ServiceDetailWindow.ShowDialog(HostServices.DialogOwner, ViewModel);
    }
}
