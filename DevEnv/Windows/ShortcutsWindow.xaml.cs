using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DevEnv.Core.Services;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Windows;

/// <summary>
/// 全屏快捷方式启动器（Launchpad 样式）：本地应用 / 文件夹 / 网站三个横向页签，
/// 顶部搜索、中部大图标网格、底部页签；单击图标即启动，Esc 或点击空白处关闭。
/// 非模态、单实例（由 <see cref="Infrastructure.ShortcutWindowHost"/> 管理）。
/// </summary>
public partial class ShortcutsWindow : Window
{
    /// <summary>打开文件对话框 / 重命名框时会先失去激活，期间不自动收起。</summary>
    private bool _suppressDeactivate;

    /// <summary>右键菜单打开期间不自动收起。</summary>
    private bool _menuOpen;

    public ShortcutsWindow()
    {
        InitializeComponent();

        AllowDrop = true;
        DragOver += OnDragOver;
        Drop += OnDrop;

        // 空白处的右键菜单也属于「弹窗」：菜单打开期间不许因失去焦点收起
        if (Root.ContextMenu is { } rootMenu)
        {
            rootMenu.Opened += (_, _) => _menuOpen = true;
            rootMenu.Closed += (_, _) => _menuOpen = false;
        }
    }

    private ShortcutLauncherViewModel ViewModel => (ShortcutLauncherViewModel)DataContext;

    /// <summary>铺满主屏并切到指定页签（apps / folders / webs），每次打开都重新读取数据。</summary>
    public void ShowPage(string? pageId)
    {
        ViewModel.Message = string.Empty;
        ViewModel.Query = string.Empty;
        ViewModel.Reload();
        ViewModel.SelectedPage = NormalizePageId(pageId);

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        var wasVisible = IsVisible;
        if (!wasVisible)
        {
            Opacity = 0;
            Show();
        }

        Activate();
        FocusSearch();

        if (wasVisible)
        {
            // 已经开着（例如拖放后再打开）就别再淡入一次，否则会闪一下
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            return;
        }

        BeginAnimation(OpacityProperty, new DoubleAnimation(0d, 1d, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    private static int NormalizePageId(string? pageId) => pageId?.Trim().ToLowerInvariant() switch
    {
        "folders" => 1,
        "webs" => 2,
        _ => 0,
    };

    private void HideLauncher()
    {
        if (!IsVisible)
            return;

        _suppressDeactivate = true;
        try
        {
            Hide();
        }
        finally
        {
            _suppressDeactivate = false;
        }

        BeginAnimation(OpacityProperty, null);
    }

    private void FocusSearch()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    #region 交互

    /// <summary>
    /// 图标单击启动。设置里开了「双击启动快捷方式」时按双击次数判断，
    /// 与原先三页卡片的行为保持一致；默认单击即启动（Launchpad 习惯）。
    /// </summary>
    private void OnEntryMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LauncherEntry entry })
            return;
        if (CoreServices.Settings.GetConfig().DoubleClickToLaunch && e.ClickCount < 2)
            return;
        LaunchEntry(entry);
    }

    private void LaunchEntry(LauncherEntry entry)
    {
        // 清掉上一次操作的残留提示，避免「启动成功却因为旧错误不收起」
        ViewModel.Message = string.Empty;
        ViewModel.Launch(entry);
        if (string.IsNullOrEmpty(ViewModel.Message))
            HideLauncher();
    }

    private void OnEntryContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LauncherEntry entry } target)
        {
            e.Handled = true;
            return;
        }

        var menu = new ContextMenu();

        if (entry.Kind == ShortcutKind.Web)
            menu.Items.Add(MenuItemFor("在浏览器中打开", () => LaunchEntry(entry)));
        else
            menu.Items.Add(MenuItemFor(entry.Kind == ShortcutKind.Folder ? "打开" : "运行", () => LaunchEntry(entry)));

        if (entry.Kind != ShortcutKind.Web)
        {
            menu.Items.Add(MenuItemFor(
                entry.Kind == ShortcutKind.App ? "在资源管理器中显示" : "在资源管理器中打开",
                () => { ViewModel.Reveal(entry); HideLauncher(); }));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor("编辑别名与图标", () => _ = RunDialogAsync(() => ViewModel.RenameAsync(entry, this))));
        if (entry.Kind != ShortcutKind.Folder)
            menu.Items.Add(MenuItemFor("刷新图标", () => _ = RunDialogAsync(() => ViewModel.RefreshIconAsync(entry))));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor("移除", () => RunDialog(() => ViewModel.Remove(entry, this))));

        ShowMenu(menu, target);
        e.Handled = true;
    }

    private static MenuItem MenuItemFor(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private void ShowMenu(ContextMenu menu, FrameworkElement target)
    {
        _menuOpen = true;
        menu.Closed += (_, _) => _menuOpen = false;
        menu.PlacementTarget = target;
        menu.IsOpen = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                // 搜索词先在 Esc 时清掉，再按一次才关闭（与 Launchpad 一致）
                if (!string.IsNullOrEmpty(ViewModel.Query))
                    ViewModel.Query = string.Empty;
                else
                    HideLauncher();
                break;

            case Key.F5:
                e.Handled = true;
                ViewModel.Reload();
                break;

            case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                e.Handled = true;
                FocusSearch();
                break;

            case Key.Left:
            case Key.Right:
                // 搜索框里左右键要移动光标，不切页
                if (ViewModel.IsSearching)
                    break;
                e.Handled = true;
                ViewModel.SelectedPage = e.Key == Key.Left
                    ? (ViewModel.SelectedPage + 2) % 3
                    : (ViewModel.SelectedPage + 1) % 3;
                break;

            case Key.Enter:
                // 回车：先启动键盘聚焦的那张卡片，否则启动第一个结果
                if (Keyboard.FocusedElement is FrameworkElement { DataContext: LauncherEntry focused })
                {
                    e.Handled = true;
                    LaunchEntry(focused);
                }
                else if (Keyboard.FocusedElement is not ButtonBase && ViewModel.VisibleEntries.Count > 0)
                {
                    e.Handled = true;
                    LaunchEntry(ViewModel.VisibleEntries[0]);
                }
                break;
        }
    }

    /// <summary>点击空白处关闭；图标、搜索框、页签与滚动条不算空白。</summary>
    private void OnRootPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;
        if (FindAncestor<ButtonBase>(source) is not null
            || FindAncestor<TextBoxBase>(source) is not null
            || FindAncestor<ListBoxItem>(source) is not null
            || FindAncestor<ScrollBar>(source) is not null
            || IsInside(source, SearchShell))
            return;

        HideLauncher();
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
                return match;
            node = ParentOf(node);
        }
        return null;
    }

    private static bool IsInside(DependencyObject? node, DependencyObject container)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, container))
                return true;
            node = ParentOf(node);
        }
        return false;
    }

    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is Visual visual
            ? VisualTreeHelper.GetParent(visual)
            : LogicalTreeHelper.GetParent(node);

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_suppressDeactivate || _menuOpen || !IsVisible)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (IsVisible && !IsActive && !_suppressDeactivate && !_menuOpen)
                HideLauncher();
        }), DispatcherPriority.Input);
    }

    /// <summary>
    /// 弹窗期间压制「失去焦点即收起」，并临时取消置顶：
    /// 启动器铺满全屏且置顶时，没有 owner 的系统文件对话框会被它挡住。
    /// </summary>
    private async Task RunDialogAsync(Func<Task> action)
    {
        _suppressDeactivate = true;
        Topmost = false;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ViewModel.Message = ex.Message;
        }
        finally
        {
            Topmost = true;
            _suppressDeactivate = false;
            if (IsVisible)
            {
                Activate();
                FocusSearch();
            }
        }
    }

    private void RunDialog(Action action)
    {
        _suppressDeactivate = true;
        Topmost = false;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ViewModel.Message = ex.Message;
        }
        finally
        {
            Topmost = true;
            _suppressDeactivate = false;
            if (IsVisible)
            {
                Activate();
                FocusSearch();
            }
        }
    }

    #endregion

    #region 添加与拖放

    private void OnAddClick(object sender, RoutedEventArgs e) =>
        _ = RunDialogAsync(() => ViewModel.AddForCurrentPageAsync(this));

    private void OnAddAppMenu(object sender, RoutedEventArgs e) => RunDialog(ViewModel.AddApp);

    private void OnAddFolderMenu(object sender, RoutedEventArgs e) => RunDialog(ViewModel.AddFolder);

    private void OnAddWebMenu(object sender, RoutedEventArgs e) =>
        _ = RunDialogAsync(() => ViewModel.AddWebAsync(this));

    private void OnReloadMenu(object sender, RoutedEventArgs e) => ViewModel.Reload();

    private void OnCloseMenu(object sender, RoutedEventArgs e) => HideLauncher();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        foreach (var file in files)
        {
            if (!string.IsNullOrWhiteSpace(file))
                ViewModel.AddDropped(file);
        }

        FocusSearch();
        e.Handled = true;
    }

    #endregion
}
