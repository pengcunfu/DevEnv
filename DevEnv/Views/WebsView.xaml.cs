using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DevEnv.Core.Models;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>网站页。卡片单击 / 双击按「双击启动快捷方式」设置决定是否在浏览器打开。</summary>
public partial class WebsView : UserControl
{
    public WebsView()
    {
        InitializeComponent();
        DataContext = new WebsViewModel();
    }

    private WebsViewModel ViewModel => (WebsViewModel)DataContext;



    /// <summary>卡片右键菜单：按 Tag 分派，理由同 AppsView。</summary>
    private void OnCardMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string action, Parent: ContextMenu menu })
            return;
        var web = (menu.PlacementTarget as FrameworkElement)?.DataContext as WebItem;
        switch (action)
        {
            case "open": ViewModel.Open(web); break;
            case "rename": _ = ViewModel.RenameAsync(web); break;
            case "refreshIcon": _ = ViewModel.RefreshIconAsync(web); break;
            case "remove": ViewModel.Remove(web); break;
            case "add": _ = ViewModel.AddAsync(); break;
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
}