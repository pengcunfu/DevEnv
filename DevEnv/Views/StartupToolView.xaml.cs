using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DevEnv.Host.Views;

/// <summary>启动项管理。</summary>
public partial class StartupToolView : UserControl
{
    public StartupToolView() => InitializeComponent();

    /// <summary>右键先把指针下的行选中，再弹出该行的删除菜单。</summary>
    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject source)
            return;

        if (ItemsControl.ContainerFromElement(grid, source) is DataGridRow row)
            row.IsSelected = true;
    }
}