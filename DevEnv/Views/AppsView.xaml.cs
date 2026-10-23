using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DevEnv.Core.Models;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>本地应用页。卡片单击 / 双击按「双击启动快捷方式」设置决定是否启动。</summary>
public partial class AppsView : UserControl
{
    public AppsView()
    {
        InitializeComponent();
        DataContext = new AppsViewModel();
    }

    private AppsViewModel ViewModel => (AppsViewModel)DataContext;



    /// <summary>
    /// 卡片右键菜单。ContextMenu 不在可视树里、且显示时 DataContext 会被 WPF 覆盖成
    /// PlacementTarget 的（条目），所以这里不用 Command 绑定，改为按 Tag 显式分派。
    /// </summary>
    private void OnCardMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string action, Parent: ContextMenu menu })
            return;
        var app = (menu.PlacementTarget as FrameworkElement)?.DataContext as NativeAppItem;
        switch (action)
        {
            case "run": ViewModel.Run(app); break;
            case "rename": ViewModel.Rename(app); break;
            case "openFolder": ViewModel.OpenFolder(app); break;
            case "refreshIcon": ViewModel.RefreshIcon(app); break;
            case "remove": ViewModel.Remove(app); break;
            case "add": ViewModel.Add(); break;
            case "reload": ViewModel.Reload(); break;
        }
    }

    /// <summary>
    /// 卡片点击。注意：<c>Button.Click</c> 抛的是 RoutedEventArgs（没有 ClickCount），
    /// 要拿单击/双击次数必须挂在 MouseLeftButtonUp 上。
    /// </summary>
    private void OnCardMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button { Tag: not null } button)
            ViewModel.OnCardActivated(button.Tag, e.ClickCount);
    }

    public void Reload() => ViewModel.Reload();

    public bool AddFromDrop(string path) => ViewModel.AddFromDrop(path);
}