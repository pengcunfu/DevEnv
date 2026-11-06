using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DevEnv.Core.Services;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Windows;

/// <summary>
/// 全屏全局启动器（Launchpad 样式）：本地应用 / 文件夹 / 网站三个横向页签，
/// 顶部搜索、中部大图标网格、底部页签；单击图标即启动，Esc 或点击空白处关闭。
/// 非模态、单实例（由 <see cref="Infrastructure.ShortcutWindowHost"/> 管理）。
/// </summary>
public partial class ShortcutsWindow : Window
{
    /// <summary>打开文件对话框 / 重命名框时会先失去激活，期间不自动收起。</summary>
    private bool _suppressDeactivate;

    /// <summary>右键菜单打开期间不自动收起。</summary>
    private bool _menuOpen;

    /// <summary>预热期间（屏幕外显示一次又隐藏）不处理失焦收起。</summary>
    private bool _prewarming;

    /// <summary>正在淡出、尚未真正隐藏。</summary>
    private bool _hiding;

    /// <summary>是否已经预热过：预热后视觉树已构建，首次打开不再临时创建。</summary>
    public bool IsPrewarmed { get; private set; }

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

    /// <summary>
    /// 把窗口在内存里准备好：在屏幕外以完全透明、不抢焦点的状态显示一次再隐藏，
    /// 让 WPF 完成 HWND 创建、模板加载、布局与首帧渲染。
    ///
    /// 不预热的话，首次打开时这些工作都在“已经可见”之后才做，表现出来就是
    /// 先闪一下空白、再闪一下内容。预热之后 Show() 只是把已经画好的窗口摆出来。
    /// </summary>
    public void Prewarm()
    {
        if (IsPrewarmed || IsVisible)
            return;

        IsPrewarmed = true;
        _prewarming = true;
        var showActivated = ShowActivated;
        try
        {
            ShowActivated = false;
            // 用最终尺寸/位置预热：显示时不再挪窗口，DWM 也就没有重新合成可看
            ApplyScreenBounds();

            // 内容保持全透明（不碰 Window.Opacity）。
            // 分层窗口会把最后一次合成结果缓存下来：若预热时把内容画成不透明，
            // 下次 Show 的头一帧就是那张不透明缓存 —— 这正是「先闪一下」的来源。
            Root.BeginAnimation(OpacityProperty, null);
            Root.Opacity = 0;

            Show();
            UpdateLayout();
            // 强制走完一次渲染，确保首帧真的画过（UpdateLayout 只排版不绘制）
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);
        }
        catch
        {
            // 预热失败不影响正常打开（只是首帧慢一点），忽略即可
        }
        finally
        {
            // 保持 Root.Opacity = 0 再隐藏：缓存的表层是全透明的，Show 不会闪
            Hide();
            ShowActivated = showActivated;
            _prewarming = false;
        }
    }

    /// <summary>
    /// 只在尺寸/位置确实不同时才改。显示过程中改窗口几何会让 DWM 重新合成，
    /// 肉眼就是闪一下；预热已经是最终几何后，这里通常一次都不改。
    /// </summary>
    private void ApplyScreenBounds()
    {
        var width = SystemParameters.PrimaryScreenWidth;
        var height = SystemParameters.PrimaryScreenHeight;

        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;

        // 注意 NaN：新窗口没设过位置时 Left/Top 是 NaN，而 "Math.Abs(NaN) > 0.5" 恒为 false，
        // 漏掉这个判断会让窗口带着 NaN 显示，由 WPF 临时挑一个位置（屏幕中间偏一点），
        // 表现出来就是显示时窗口先出现在错误位置再跳过来。
        if (double.IsNaN(Left) || Math.Abs(Left) > 0.5)
            Left = 0;
        if (double.IsNaN(Top) || Math.Abs(Top) > 0.5)
            Top = 0;
        if (double.IsNaN(Width) || Math.Abs(Width - width) > 0.5)
            Width = width;
        if (double.IsNaN(Height) || Math.Abs(Height - height) > 0.5)
            Height = height;
    }

    /// <summary>铺满主屏并切到指定页签（apps / folders / webs），每次打开都重新读取数据。</summary>
    public void ShowPage(string? pageId)
    {
        ViewModel.Message = string.Empty;
        ViewModel.Query = string.Empty;
        ViewModel.Reload();
        ViewModel.SelectedPage = NormalizePageId(pageId);

        var wasVisible = IsVisible;
        if (_hiding)
        {
            // 正在淡出时又要求打开：取消这次收起，直接淡回全不透明
            _hiding = false;
            _suppressDeactivate = false;
            wasVisible = true;
        }

        if (!wasVisible)
        {
            // 没预热过就先补一次：Show 之后再布局会看到一帧空白
            if (!IsPrewarmed)
                Prewarm();

            ApplyScreenBounds();
            // 淡入做在根元素上而不是 Window.Opacity：分层窗口改自身透明度
            // 会先按不透明画一帧再被拉回 0，看起来就是闪
            Root.BeginAnimation(OpacityProperty, null);
            Root.Opacity = 0;
            Show();
        }

        _shownAtUtc = DateTime.UtcNow;
        BringToForeground();
        FocusSearch();

        if (wasVisible)
        {
            // 已经开着（例如拖放后再打开）就别再淡入一次，否则会闪一下
            Root.BeginAnimation(OpacityProperty, null);
            Root.Opacity = 1;
            return;
        }

        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, 1d, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    /// <summary>
    /// 把窗口拉到前台，但只做一次：
    /// WPF 的 Activate() 是异步的，刚调用完 IsActive 仍可能是 false，
    /// 据此再补一次 ForceForeground 会让窗口被反复激活、重绘（表现为闪一下）。
    /// 这里直接比较系统的前台窗口句柄，只有确实不是自己时才强制前置。
    /// </summary>
    private void BringToForeground()
    {
        Activate();
        if (GetForegroundWindow() == new WindowInteropHelper(this).Handle)
            return;
        // 从全局 Win 键 / 托盘唤起时本进程没有前台权限，普通 Activate 会被系统拒绝
        SigXor.Services.WindowPlacement.ForceForeground(this);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static int NormalizePageId(string? pageId) => pageId?.Trim().ToLowerInvariant() switch
    {
        "folders" => 1,
        "webs" => 2,
        _ => 0,
    };

    /// <summary>正在淡出、尚未真正隐藏（此刻再按 Win / 点托盘应按「重新打开」处理）。</summary>
    public bool IsHiding => _hiding;

    /// <summary>从外部收起（Win 键再次按下、托盘入口等）。</summary>
    public void Dismiss() => HideLauncher();

    /// <summary>因失去焦点而自动收起时触发；宿主据此判断紧接着的托盘点击是「关闭」。</summary>
    public event Action? AutoDismissed;

    /// <summary>
    /// 刚显示的一小段时间内忽略「失去焦点就收起」：
    /// 从别的程序按下 Win 唤起时，激活要等系统把前台权限交过来，
    /// 这段空窗里会先收到一次 Deactivated，照常处理就会显示后立刻收起（又是一次闪）。
    /// </summary>
    private static readonly TimeSpan FocusGrace = TimeSpan.FromMilliseconds(400);
    private DateTime _shownAtUtc;

    /// <summary>
    /// 收起：先淡出到全透明，**在可见状态下**完成这一帧渲染，再真正 Hide。
    ///
    /// 直接 Hide 后再把内容设成全透明是没用的——窗口已经不可见，不会再渲染，
    /// 分层窗口缓存里留着的仍是不透明画面，下次 Show 的头一帧就会把它贴出来。
    /// 这就是「第一次打开正常、之后每次打开都闪一下」的原因。
    /// </summary>
    private void HideLauncher()
    {
        if (!IsVisible || _hiding)
            return;

        _suppressDeactivate = true;
        _hiding = true;

        var fade = new DoubleAnimation(0d, TimeSpan.FromMilliseconds(110))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        fade.Completed += (_, _) =>
        {
            Root.BeginAnimation(OpacityProperty, null);
            Root.Opacity = 0;
            try
            {
                Hide();
            }
            finally
            {
                _suppressDeactivate = false;
                _hiding = false;
            }
        };
        Root.BeginAnimation(OpacityProperty, fade);
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
        if (_suppressDeactivate || _prewarming || _menuOpen || !IsVisible)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsVisible || !IsActive || _suppressDeactivate || _menuOpen)
                return;
            if (DateTime.UtcNow - _shownAtUtc < FocusGrace)
                return;

            HideLauncher();
            AutoDismissed?.Invoke();
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
