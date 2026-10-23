using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DevBox.Core.Models;
using DevBox.Host.ViewModels;

namespace DevBox.Host.Views;

/// <summary>文件夹页。卡片单击 / 双击按「双击启动快捷方式」设置决定是否打开。</summary>
public partial class FoldersView : UserControl
{
    public FoldersView()
    {
        InitializeComponent();
        DataContext = new FoldersViewModel();
    }

    private FoldersViewModel ViewModel => (FoldersViewModel)DataContext;



    /// <summary>卡片右键菜单：按 Tag 分派，理由同 AppsView。</summary>
    private void OnCardMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string action, Parent: ContextMenu menu })
            return;
        var folder = (menu.PlacementTarget as FrameworkElement)?.DataContext as FolderItem;
        switch (action)
        {
            case "open": ViewModel.Open(folder); break;
            case "rename": ViewModel.Rename(folder); break;
            case "remove": ViewModel.Remove(folder); break;
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