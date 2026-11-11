using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.Core.Models;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>端口管理：离开页面时停掉定时器；右键 / 双击打开详情。</summary>
public partial class PortsView : UserControl
{
    public PortsView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as IDisposable)?.Dispose();
    }

    private PortsViewModel ViewModel => (PortsViewModel)DataContext;

    /// <summary>右键先选中行，再弹出菜单。</summary>
    private void OnGridPreviewRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (VisualTreeSearch.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is PortRowView row)
            ViewModel.Selected = row;
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.ShowDetailCommand.CanExecute(null))
            ViewModel.ShowDetailCommand.Execute(null);
    }
}
