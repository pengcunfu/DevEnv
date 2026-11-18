using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Windows;

/// <summary>
/// 启动器（主窗口）：只承载「系统工具 / 内置工具」两个页签。
/// 本地应用 / 文件夹 / 网站 / 开始菜单已拆成独立窗口（见 <see cref="Views.ShortcutCategoryView"/>），
/// 从内置工具页的「窗口入口」打开。
/// 非模态、单实例（由 <see cref="ShortcutWindowHost"/> 管理），左 Alt / 托盘唤起。
/// </summary>
public partial class ShortcutsWindow : Window
{
    /// <summary>浅色 / 深色两套启动器配色，与开始菜单保持一致。</summary>
    private static readonly Uri LauncherLightPalette =
        new("/DevEnv;component/Themes/Launcher.Light.xaml", UriKind.Relative);

    private static readonly Uri LauncherDarkPalette =
        new("/DevEnv;component/Themes/Launcher.Dark.xaml", UriKind.Relative);

    /// <summary>打开对话框 / 右键菜单期间不自动收起。</summary>
    private bool _suppressDeactivate;
    private bool _menuOpen;

    /// <summary>预热期间（透明显示一次又隐藏）不处理失焦收起。</summary>
    private bool _prewarming;

    /// <summary>是否已经预热过：预热后视觉树已构建，首次打开不再临时创建。</summary>
    public bool IsPrewarmed { get; private set; }

    /// <summary>启动器可见期间监听 Win 键：用户要打开开始菜单时先把悬浮窗口收起。</summary>
    private readonly WinKeyDismissHook _winKeyHook;

    /// <summary>启动器可见期间监听窗口外点击：补上不会触发 Deactivated 的点击场景。</summary>
    private readonly OutsideClickDismissHook _outsideClickHook;

    /// <summary>DEVENV_START_PAGE=tools：数据落定后自动展开「系统工具」菜单。</summary>
    private bool _openSystemToolsMenuPending;

    public ShortcutsWindow()
    {
        InitializeComponent();
        ApplySystemColorScheme();

        ViewModel.SystemToolMenus.CollectionChanged += (_, _) => TryOpenSystemToolsMenu();

        // 菜单弹出层在窗口外：打开期间要压制「点窗口外 / 失焦就收起」，
        // 否则点第一下菜单项就会把启动器藏起来（和右键菜单同一处理）。
        SystemToolsMenuItem.SubmenuOpened += (_, _) => _menuOpen = true;
        SystemToolsMenuItem.SubmenuClosed += (_, _) => _menuOpen = false;

        // 卡片模板放在共用 ResourceDictionary 里，事件统一挂在网格上靠冒泡处理
        SectionGrid.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnEntryClick));
        SectionGrid.AddHandler(ContextMenuService.ContextMenuOpeningEvent,
            new ContextMenuEventHandler(OnEntryContextMenuOpening));

        _winKeyHook = new WinKeyDismissHook(Dispatcher);
        _winKeyHook.WindowsKeyPressed += OnWindowsKeyPressed;

        _outsideClickHook = new OutsideClickDismissHook(
            Dispatcher,
            () => new WindowInteropHelper(this).Handle,
            () => _suppressDeactivate || _menuOpen || HasVisibleOwnedChildWindow());
        _outsideClickHook.OutsideClicked += OnOutsideClicked;

        Closed += (_, _) =>
        {
            _winKeyHook.Dispose();
            _outsideClickHook.Dispose();
        };

        if (Root.ContextMenu is { } rootMenu)
        {
            rootMenu.Opened += (_, _) => _menuOpen = true;
            rootMenu.Closed += (_, _) => _menuOpen = false;
        }
    }

    private ShortcutLauncherViewModel ViewModel => (ShortcutLauncherViewModel)DataContext;

    /// <summary>
    /// 把窗口在内存里准备好：在最终位置以完全透明、不抢焦点的状态显示一次再隐藏，
    /// 让 WPF 完成 HWND 创建、模板加载、布局与首帧渲染，首次打开不会先闪空白。
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
            ApplyScreenBounds();
            Opacity = 0;

            Show();
            UpdateLayout();
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);
        }
        catch
        {
            // 预热失败不影响正常打开（只是首帧慢一点），忽略即可
        }
        finally
        {
            Hide();
            Opacity = 1;
            ShowActivated = showActivated;
            _prewarming = false;
        }
    }

    /// <summary>默认 960 × 680 DIP，在工作区内水平、垂直居中；只在几何确实不同时才改。</summary>
    private void ApplyScreenBounds()
    {
        var bounds = ComputeScreenBounds();

        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;

        if (double.IsNaN(Left) || Math.Abs(Left - bounds.Left) > 0.5)
            Left = bounds.Left;
        if (double.IsNaN(Top) || Math.Abs(Top - bounds.Top) > 0.5)
            Top = bounds.Top;
        if (double.IsNaN(Width) || Math.Abs(Width - bounds.Width) > 0.5)
            Width = bounds.Width;
        if (double.IsNaN(Height) || Math.Abs(Height - bounds.Height) > 0.5)
            Height = bounds.Height;
    }

    /// <summary>启动器的目标屏幕区域（DIP）；自检探针用同一份计算做断言。</summary>
    internal static Rect ComputeScreenBounds() => CursorScreen.GetLauncherBoundsDip();

    /// <summary>启动器配色跟随 Windows 的开始菜单：读取系统「应用模式」。</summary>
    private void ApplySystemColorScheme()
    {
        var target = SystemUsesLightAppTheme() ? LauncherLightPalette : LauncherDarkPalette;
        var merged = Resources.MergedDictionaries;
        for (var i = 0; i < merged.Count; i++)
        {
            if (!IsLauncherPalette(merged[i]))
                continue;

            if (!string.Equals(
                    merged[i].Source?.OriginalString,
                    target.OriginalString,
                    StringComparison.OrdinalIgnoreCase))
            {
                merged[i] = new ResourceDictionary { Source = target };
            }
            return;
        }

        merged.Insert(0, new ResourceDictionary { Source = target });
    }

    private static bool IsLauncherPalette(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        return source is not null
            && (source.EndsWith("Launcher.Light.xaml", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("Launcher.Dark.xaml", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>系统「应用模式」是否为浅色；读不到时按浅色处理。</summary>
    private static bool SystemUsesLightAppTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is null || Convert.ToInt32(value) != 0;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// 显示启动器并切到 tools / builtin 页签；<c>pageId</c> 为空则保留上次页签。
    /// apps / folders / webs / startmenu 已在 <see cref="ShortcutWindowHost"/> 里转到独立窗口。
    /// </summary>
    public void ShowPage(string? pageId)
    {
        ApplySystemColorScheme();

        ViewModel.Message = string.Empty;
        ViewModel.Notice = string.Empty;
        ViewModel.Reload();
        if (!string.IsNullOrWhiteSpace(pageId))
            ViewModel.SelectedPage = NormalizePageId(pageId);

        // tools 路由不再有页签可切，改为展开顶部「系统工具」菜单
        _openSystemToolsMenuPending = string.Equals(
            pageId?.Trim().TrimStart('/'), "tools", StringComparison.OrdinalIgnoreCase);

        if (!IsVisible)
        {
            if (!IsPrewarmed)
                Prewarm();

            ApplyScreenBounds();
            Show();
        }

        _winKeyHook.Install();
        _outsideClickHook.Install();

        _shownAtUtc = DateTime.UtcNow;
        BringToForeground();
        TryOpenSystemToolsMenu();
    }

    /// <summary>数据落定且窗口可见后展开系统工具菜单；WPF 弹出菜单挂在窗口上，隐藏时打不开。</summary>
    private bool TryOpenSystemToolsMenu()
    {
        if (!_openSystemToolsMenuPending || ViewModel.SystemToolMenus.Count == 0 || !IsVisible)
            return false;

        _openSystemToolsMenuPending = false;
        SystemToolsMenuItem.IsSubmenuOpen = true;
        return true;
    }

    /// <summary>把窗口拉到前台，但只做一次，避免反复激活导致闪烁。</summary>
    private void BringToForeground()
    {
        Activate();
        if (GetForegroundWindow() == new WindowInteropHelper(this).Handle)
            return;
        SigXor.Services.WindowPlacement.ForceForeground(this);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>启动器只剩内置工具一页；tools 路由由 <see cref="ShowPage"/> 额外展开菜单。</summary>
    private static int NormalizePageId(string? pageId) => 4;

    /// <summary>从外部收起（Win 键再次按下、托盘入口等）。</summary>
    public void Dismiss() => HideLauncher();

    /// <summary>因失去焦点而自动收起时触发；宿主据此判断紧接着的托盘点击是「关闭」。</summary>
    public event Action? AutoDismissed;

    /// <summary>刚显示的一小段时间内忽略「失去焦点就收起」，等系统把前台权限交过来。</summary>
    private static readonly TimeSpan FocusGrace = TimeSpan.FromMilliseconds(400);
    private DateTime _shownAtUtc;

    private void HideLauncher()
    {
        if (!IsVisible)
            return;

        _suppressDeactivate = true;
        try
        {
            Hide();
            _winKeyHook.Uninstall();
            _outsideClickHook.Uninstall();
        }
        finally
        {
            _suppressDeactivate = false;
        }
    }

    private void OnOutsideClicked()
    {
        if (!IsVisible || _suppressDeactivate || _menuOpen || HasVisibleOwnedChildWindow()
            || WindowState == WindowState.Minimized)
            return;

        HideLauncher();
        AutoDismissed?.Invoke();
    }

    /// <summary>自检探针用：窗口外点击监听是否处于安装状态。</summary>
    internal bool IsOutsideClickHookInstalled => _outsideClickHook.IsInstalled;

    /// <summary>自检探针用：模拟一次窗口外点击，走与钩子相同的收起路径。</summary>
    internal void SimulateOutsideClick() => OnOutsideClicked();

    private void OnWindowsKeyPressed()
    {
        if (!IsVisible || _suppressDeactivate || WindowState == WindowState.Minimized)
            return;

        HideLauncher();
    }

    #region 交互

    /// <summary>图标单击启动；事件挂在网格上，由模板按钮冒泡上来。主界面保持打开。</summary>
    private void OnEntryClick(object sender, RoutedEventArgs e)
    {
        if (FindEntry(e.OriginalSource as DependencyObject) is not { } entry)
            return;

        ViewModel.Message = string.Empty;
        ViewModel.Launch(entry);
    }

    private void OnEntryContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (FindEntry(e.OriginalSource as DependencyObject) is not { } entry)
        {
            // 空白处交给 Root 自己的刷新 / 关闭菜单
            return;
        }

        var menu = new ContextMenu();
        var run = new MenuItem { Header = "运行" };
        run.Click += (_, _) =>
        {
            ViewModel.Message = string.Empty;
            ViewModel.Launch(entry);
        };
        menu.Items.Add(run);

        _menuOpen = true;
        menu.Closed += (_, _) => _menuOpen = false;
        menu.PlacementTarget = e.OriginalSource as FrameworkElement;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static LauncherEntry? FindEntry(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: LauncherEntry entry })
                return entry;

            source = source is Visual visual
                ? VisualTreeHelper.GetParent(visual)
                : LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                HideLauncher();
                break;

            case Key.F5:
                e.Handled = true;
                ViewModel.Reload(forceStartMenuRefresh: true);
                break;

            case Key.Enter:
                if (Keyboard.FocusedElement is FrameworkElement { DataContext: LauncherEntry focused })
                {
                    e.Handled = true;
                    ViewModel.Launch(focused);
                }
                else if (Keyboard.FocusedElement is not ButtonBase && ViewModel.VisibleEntries.Count > 0)
                {
                    e.Handled = true;
                    ViewModel.Launch(ViewModel.VisibleEntries[0]);
                }
                break;
        }
    }

    /// <summary>系统工具菜单项单击：运行对应工具，主界面保持打开。</summary>
    private void OnSystemToolMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: LauncherEntry entry })
            return;

        ViewModel.Message = string.Empty;
        ViewModel.Launch(entry);
    }

    /// <summary>
    /// 从主界面打开的子窗口（内置工具窗 / 分类窗口 / 翻译 / 剪贴板）还开着时，
    /// 主界面保持可见，不因失焦或点击子窗口而收起。
    /// </summary>
    private bool HasVisibleOwnedChildWindow() =>
        Application.Current?.Windows
            .OfType<Window>()
            .Any(window => !ReferenceEquals(window, this)
                && window.IsVisible
                && ReferenceEquals(window.Owner, this)) == true;

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            return;

        if (_suppressDeactivate || _prewarming || _menuOpen
            || HasVisibleOwnedChildWindow() || !IsVisible)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsVisible || !IsActive || _suppressDeactivate || _menuOpen
                || HasVisibleOwnedChildWindow()
                || WindowState == WindowState.Minimized)
                return;
            if (DateTime.UtcNow - _shownAtUtc < FocusGrace)
                return;

            HideLauncher();
            AutoDismissed?.Invoke();
        }), DispatcherPriority.Input);
    }

    /// <summary>
    /// 最小化后卸载窗口外点击监听，避免点击任务栏恢复时被当成「点外面」又收起；
    /// 还原后重新安装，并重置激活宽限。
    /// </summary>
    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            _winKeyHook.Uninstall();
            _outsideClickHook.Uninstall();
            return;
        }

        if (!IsVisible)
            return;

        _winKeyHook.Install();
        _outsideClickHook.Install();
        _shownAtUtc = DateTime.UtcNow;
    }

    #endregion

    #region 右键菜单

    private void OnReloadMenu(object sender, RoutedEventArgs e) =>
        ViewModel.Reload(forceStartMenuRefresh: true);

    private void OnCloseMenu(object sender, RoutedEventArgs e) => HideLauncher();

    #endregion
}
