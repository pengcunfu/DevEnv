using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DevEnv.Host.Views;

/// <summary>右键菜单管理（在独立工具窗中打开）。</summary>
public partial class ContextMenuView : UserControl
{
    public ContextMenuView() => InitializeComponent();

    /// <summary>右键先把指针下的行选中，再弹出该行的启用 / 删除菜单。</summary>
    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject source)
            return;

        if (ItemsControl.ContainerFromElement(grid, source) is DataGridRow row)
            row.IsSelected = true;
    }
}