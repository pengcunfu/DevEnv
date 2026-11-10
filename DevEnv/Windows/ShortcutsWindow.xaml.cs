using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Windows;

/// <summary>
/// 启动器（Launchpad 样式）：固定 960 × 680 DIP、居中，配色与 Windows 11 开始菜单一致。
/// 左侧栏自上而下是搜索框与本地应用 / 文件夹 / 网站 / 系统工具 / 内置工具 / 开始菜单六个纵向页签
/// 与系统工具 / 内置工具两个只读文件夹；中部大图标网格；
/// 单击图标即启动，Esc 或点击空白处关闭。
/// 非模态、单实例（由 <see cref="Infrastructure.ShortcutWindowHost"/> 管理）。
/// </summary>
public partial class ShortcutsWindow : Window
{
    /// <summary>浅色 / 深色两套启动器配色，与开始菜单保持一致。</summary>
    private static readonly Uri LauncherLightPalette =
        new("/DevEnv;component/Themes/Launcher.Light.xaml", UriKind.Relative);

    private static readonly Uri LauncherDarkPalette =
        new("/DevEnv;component/Themes/Launcher.Dark.xaml", UriKind.Relative);

    /// <summary>打开文件对话框 / 重命名框时会先失去激活，期间不自动收起。</summary>
    private bool _suppressDeactivate;

    /// <summary>右键菜单打开期间不自动收起。</summary>
    private bool _menuOpen;

    /// <summary>预热期间（透明显示一次又隐藏）不处理失焦收起。</summary>
    private bool _prewarming;

    /// <summary>启动器内部拖拽的数据格式；不与外部文件拖入的 FileDrop 混用。</summary>
    private const string EntryDragFormat = "DevEnv.LauncherEntries";

    /// <summary>
    /// 按下后移动多少 DIP 才视为拖拽。系统默认约 3.2，鼠标单击时轻微抖动就会
    /// 误判成拖拽而丢掉这次点击；启动器里拖拽是管理操作，用更保守的阈值。
    /// </summary>
    private const double DragStartThreshold = 8;

    /// <summary>框选起点与状态。</summary>
    private Point _marqueeStart;
    private bool _marqueePressed;
    private bool _marqueeActive;
    private bool _marqueeCtrl;
    private List<LauncherEntry> _marqueeSnapshot = [];

    /// <summary>卡片按下 / 拖动状态。</summary>
    private LauncherEntry? _entryPress;
    private Point _entryPressPoint;
    private bool _entryPressCtrl;
    private bool _dragActive;

    /// <summary>拖拽结束后的短暂点击抑制，避免补发的 MouseUp 误启动卡片。</summary>
    private DateTime _suppressEntryClickUntilUtc;

    /// <summary>当前拖拽落点；DragOver 只更新它，Drop 时才真正修改布局。</summary>
    private DropPlan? _dropPlan;

    private enum DropMode
    {
        Insert,
        Merge,
    }

    private sealed record DropPlan(DropMode Mode, LauncherEntry? Target, int InsertIndex, Rect? TargetRect);

    /// <summary>是否已经预热过：预热后视觉树已构建，首次打开不再临时创建。</summary>
    public bool IsPrewarmed { get; private set; }

    /// <summary>启动器可见期间监听 Win 键：用户要打开开始菜单时先把悬浮窗口收起。</summary>
    private readonly WinKeyDismissHook _winKeyHook;

    /// <summary>启动器可见期间监听窗口外点击：补上不会触发 Deactivated 的点击场景。</summary>
    private readonly OutsideClickDismissHook _outsideClickHook;

    /// <summary>换页时网格的横向位移；只做渲染变换，不影响布局与框选坐标。</summary>
    private readonly TranslateTransform _pageSlide = new();

    /// <summary>文件夹浮层的弹出缩放。</summary>
    private readonly ScaleTransform _folderScale = new(1, 1);

    /// <summary>上一页的索引，用来判断换页方向（循环时 4 → 0 算下一页）。</summary>
    private int _lastPageIndex;

    /// <summary>最近一次换页的时间；卡片容器在这之后的一小段时间内加载才做错峰入场。</summary>
    private DateTime _entranceAtUtc = DateTime.MinValue;

    public ShortcutsWindow()
    {
        InitializeComponent();
        ApplySystemColorScheme();

        AllowDrop = true;
        DragOver += OnDragOver;
        DragLeave += OnDragLeave;
        Drop += OnDrop;

        PageContentHost.RenderTransform = _pageSlide;
        FolderCard.RenderTransform = _folderScale;
        FolderCard.RenderTransformOrigin = new Point(0.5, 0.5);

        _lastPageIndex = ViewModel.SelectedPage;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        _winKeyHook = new WinKeyDismissHook(Dispatcher);
        _winKeyHook.WindowsKeyPressed += OnWindowsKeyPressed;

        // 失焦收起覆盖不到所有点击（任务栏空白、窗口尚未激活等），再监听一层窗口外点击
        _outsideClickHook = new OutsideClickDismissHook(
            Dispatcher,
            () => new WindowInteropHelper(this).Handle,
            () => _suppressDeactivate || _menuOpen || _dragActive || _marqueeActive);
        _outsideClickHook.OutsideClicked += OnOutsideClicked;

        Closed += (_, _) =>
        {
            _winKeyHook.Dispose();
            _outsideClickHook.Dispose();
        };

        // 空白处的右键菜单也属于「弹窗」：菜单打开期间不许因失去焦点收起
        if (Root.ContextMenu is { } rootMenu)
        {
            rootMenu.Opened += (_, _) =>
            {
                _menuOpen = true;
                UpdateMergeSelectionMenu();
            };
            rootMenu.Closed += (_, _) => _menuOpen = false;
        }
    }

    private ShortcutLauncherViewModel ViewModel => (ShortcutLauncherViewModel)DataContext;

    /// <summary>
    /// 把窗口在内存里准备好：在最终位置以完全透明、不抢焦点的状态显示一次再隐藏，
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

            // 标准窗口不能靠根元素透明来隐身（不透明窗口仍会画出背景），
            // 用 Window.Opacity 让预热这一帧完全不显示。
            Opacity = 0;

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
            Hide();
            Opacity = 1;
            ShowActivated = showActivated;
            _prewarming = false;
        }
    }

    /// <summary>
    /// 固定 960 × 680 DIP，在工作区内水平、垂直居中。
    /// 只在尺寸/位置确实不同时才改，显示过程中改窗口几何会让 DWM 重新合成，肉眼就是闪一下；
    /// 预热后这里通常一次都不改。
    /// </summary>
    private void ApplyScreenBounds()
    {
        var bounds = ComputeScreenBounds();

        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;

        // 注意 NaN：新窗口没设过位置时 Left/Top 是 NaN，而 "Math.Abs(NaN) > 0.5" 恒为 false，
        // 漏掉这个判断会让窗口带着 NaN 显示，由 WPF 临时挑一个位置（屏幕中间偏一点），
        // 表现出来就是显示时窗口先出现在错误位置再跳过来。
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
    internal static Rect ComputeScreenBounds()
    {
        var area = SystemParameters.WorkArea;
        var bounds = LauncherGeometry.Compute(
            area.Left, area.Top, area.Width, area.Height);
        return new Rect(
            Math.Round(bounds.Left),
            Math.Round(bounds.Top),
            Math.Round(bounds.Width),
            Math.Round(bounds.Height));
    }

    /// <summary>
    /// 启动器配色跟随 Windows 的开始菜单：读取系统「应用模式」<c>AppsUseLightTheme</c>，
    /// 而不是 DevEnv 自己的深色开关。两套资源 key 完全一致，整块替换即可。
    /// 每次打开前重新读取，用户在系统设置里切换浅 / 深色后无需重启程序。
    /// </summary>
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

    /// <summary>系统「应用模式」是否为浅色；读不到时按浅色处理（Windows 默认值）。</summary>
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
    /// 摆到工作区居中的位置并显示。传入 apps / folders / webs / tools / builtin / startmenu
    /// 时切到对应页签；<c>pageId</c> 为空则保留上次所在页签（托盘 / 左 Alt / 显示主窗口
    /// 再打开时不回到「本地应用」）。每次打开都重新读取数据与系统配色。
    /// </summary>
    public void ShowPage(string? pageId)
    {
        ApplySystemColorScheme();
        CancelMarquee();
        HideDropIndicators();
        _dropPlan = null;
        _suppressEntryClickUntilUtc = DateTime.MinValue;

        ViewModel.Message = string.Empty;
        ViewModel.Notice = string.Empty;
        ViewModel.Query = string.Empty;
        ViewModel.Reload();
        if (!string.IsNullOrWhiteSpace(pageId))
            ViewModel.SelectedPage = NormalizePageId(pageId);

        if (!IsVisible)
        {
            // 没预热过就先补一次：Show 之后再布局会看到一帧空白
            if (!IsPrewarmed)
                Prewarm();

            ApplyScreenBounds();
            Show();
        }

        // 启动器可见期间才监听 Win 键与窗口外点击，隐藏后卸载，不常驻在系统里
        _winKeyHook.Install();
        _outsideClickHook.Install();

        _shownAtUtc = DateTime.UtcNow;
        BringToForeground();
        FocusSearch();
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
        "tools" => 3,
        "builtin" => 4,
        "startmenu" => 5,
        _ => 0,
    };

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

    /// <summary>收起：标准窗口直接隐藏，不播退出动画，同时卸载只在该窗口可见期间需要的两个钩子。</summary>
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

    /// <summary>
    /// 点在启动器窗口之外：收起窗口。即使窗口没有被真正激活、点击落在任务栏空白处，
    /// 或系统弹出层吞掉了 Deactivated，也能按「点外面就收起」处理。
    /// </summary>
    private void OnOutsideClicked()
    {
        if (!IsVisible || _suppressDeactivate || _menuOpen || _dragActive || _marqueeActive)
            return;

        HideLauncher();
        AutoDismissed?.Invoke();
    }

    /// <summary>自检探针用：窗口外点击监听是否处于安装状态。</summary>
    internal bool IsOutsideClickHookInstalled => _outsideClickHook.IsInstalled;

    /// <summary>自检探针用：模拟一次窗口外点击，走与钩子相同的收起路径。</summary>
    internal void SimulateOutsideClick() => OnOutsideClicked();

    /// <summary>
    /// 启动器显示期间按了 Win 键：先收起自己的全屏置顶窗口，
    /// 让系统开始菜单露出来，而不是被压在启动器下面。
    /// </summary>
    private void OnWindowsKeyPressed()
    {
        // 打开文件 / 重命名对话框期间不动窗口，避免把模态对话框的宿主一起藏掉
        if (!IsVisible || _suppressDeactivate)
            return;

        HideLauncher();
    }

    private void FocusSearch()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    #region 交互

    /// <summary>
    /// 图标单击启动（Launchpad 习惯）。
    /// 用 Button.Click 而不是 MouseLeftButtonUp：ButtonBase 在鼠标捕获与拖拽打断时
    /// 对 Click 的处理最完整，也不会被手写捕获逻辑吞掉。
    /// 框选 / Ctrl+单击形成多选后，第一次点击只收窄选择，避免管理图标时误启动。
    /// </summary>
    private void OnEntryClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LauncherEntry entry })
            return;

        var pressCtrl = _entryPressCtrl;
        _entryPress = null;
        _entryPressCtrl = false;

        if (DateTime.UtcNow < _suppressEntryClickUntilUtc || _dragActive)
            return;
        if (pressCtrl || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        if (ViewModel.HasSelection)
        {
            if (!entry.IsSelected || ViewModel.SelectedEntries.Count > 1)
            {
                ViewModel.SetSelection([entry]);
                return;
            }

            ViewModel.ClearSelection();
        }

        LaunchEntry(entry);
    }

    /// <summary>卡片按下：记录起点；Ctrl+单击切换选中，不触发启动。</summary>
    private void OnEntryPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LauncherEntry entry })
            return;

        _entryPressCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (!ViewModel.IsEditableView)
        {
            _entryPress = null;
            return;
        }

        _entryPress = entry;
        _entryPressPoint = e.GetPosition(Root);

        if (_entryPressCtrl)
        {
            ViewModel.ToggleSelection(entry);
            // 切换选中后 Button 仍会补发 Click，短暂屏蔽，避免顺手启动
            _suppressEntryClickUntilUtc = DateTime.UtcNow.AddMilliseconds(400);
        }
    }

    /// <summary>卡片移动超过阈值后进入拖拽；框选形成多选时拖动整个选择。</summary>
    private void OnEntryPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragActive || _entryPress is null || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (!ViewModel.IsEditableView)
            return;

        var current = e.GetPosition(Root);
        if (Math.Abs(current.X - _entryPressPoint.X) < DragStartThreshold
            && Math.Abs(current.Y - _entryPressPoint.Y) < DragStartThreshold)
        {
            return;
        }

        var entry = _entryPress;
        _entryPress = null;

        if (!entry.IsSelected)
            ViewModel.SetSelection([entry]);
        else if (ViewModel.SelectedEntries.Count == 0)
            ViewModel.SetSelection([entry]);

        var entries = ViewModel.SelectedEntries;
        if (entries.Count == 0)
            entries = [entry];

        if (sender is UIElement element)
            element.ReleaseMouseCapture();

        _dragActive = true;
        try
        {
            DragDrop.DoDragDrop(
                (DependencyObject)sender,
                new DataObject(EntryDragFormat, entries),
                DragDropEffects.Move);
        }
        finally
        {
            _dragActive = false;
            HideDropIndicators();
            // DoDragDrop 返回后可能补发一次 MouseUp，短暂屏蔽启动
            _suppressEntryClickUntilUtc = DateTime.UtcNow.AddMilliseconds(300);
        }
    }

    private void LaunchEntry(LauncherEntry entry)
    {
        // 清掉上一次操作的残留提示，避免「启动成功却因为旧错误不收起」
        ViewModel.Message = string.Empty;

        // 文件夹磁贴只展开内容，不启动、也不收起启动器
        if (entry.IsGroup)
        {
            ViewModel.OpenGroup(entry);
            FocusSearch();
            return;
        }

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

        // 右键落在多选项上：先保持选择，显示批量菜单
        if (ViewModel.HasSelection && entry.IsSelected && ViewModel.SelectedEntries.Count > 1)
        {
            ShowBatchMenu(target);
            e.Handled = true;
            return;
        }

        var menu = new ContextMenu();

        // 开始菜单条目只读：可启动、收进启动器、定位文件，不允许改名 / 移除
        if (entry.Kind == ShortcutKind.StartMenu)
        {
            menu.Items.Add(MenuItemFor("运行", () => LaunchEntry(entry)));
            menu.Items.Add(MenuItemFor("添加到启动器", () => ViewModel.AddStartMenuEntry(entry)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor("在资源管理器中显示",
                () => { ViewModel.Reveal(entry); HideLauncher(); }));
            ShowMenu(menu, target);
            e.Handled = true;
            return;
        }

        // 开始菜单按一级文件夹分出来的只读文件夹只提供展开
        if (entry.IsGroup && entry.IsReadOnlyGroup)
        {
            menu.Items.Add(MenuItemFor("打开文件夹", () => ViewModel.OpenGroup(entry)));
            ShowMenu(menu, target);
            e.Handled = true;
            return;
        }

        // 系统工具与内置工具是固定目录，只提供「运行」，不参与改名 / 移除 / 刷新图标
        if (entry.Kind is ShortcutKind.Tool or ShortcutKind.InternalTool)
        {
            menu.Items.Add(MenuItemFor("运行", () => LaunchEntry(entry)));
            ShowMenu(menu, target);
            e.Handled = true;
            return;
        }

        if (entry.IsGroup)
        {
            menu.Items.Add(MenuItemFor("打开文件夹", () => ViewModel.OpenGroup(entry)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor("重命名文件夹",
                () => _ = RunDialogAsync(() => ViewModel.RenameAsync(entry, this))));
            menu.Items.Add(MenuItemFor("解散文件夹", () => RunDialog(() => ViewModel.DissolveGroup(entry))));
            ShowMenu(menu, target);
            e.Handled = true;
            return;
        }

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

        if (ViewModel.IsInsideGroup)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor("移出文件夹", () => RunDialog(() => ViewModel.MoveOutOfGroup([entry]))));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor("移除", () => RunDialog(() => ViewModel.Remove(entry, this))));

        ShowMenu(menu, target);
        e.Handled = true;
    }

    /// <summary>多选后的批量菜单：合并为文件夹、移出文件夹、批量移除、取消选择。</summary>
    private void ShowBatchMenu(FrameworkElement target)
    {
        var selected = ViewModel.SelectedEntries;
        var menu = new ContextMenu();

        if (ViewModel.CanCreateGroup && selected.Count >= 2 && selected.All(entry => !entry.IsGroup))
        {
            menu.Items.Add(MenuItemFor($"合并 {selected.Count} 项为文件夹…", () =>
                _ = RunDialogAsync(() => MergeSelectionAsync(selected))));
        }

        if (ViewModel.IsInsideGroup && selected.All(entry => !entry.IsGroup))
        {
            menu.Items.Add(MenuItemFor($"移出文件夹（{selected.Count} 项）",
                () => RunDialog(() => ViewModel.MoveOutOfGroup(selected))));
        }

        menu.Items.Add(MenuItemFor($"移除选中的 {selected.Count} 项…",
            () => RunDialog(() => ViewModel.RemoveSelected(this))));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor("取消选择", ViewModel.ClearSelection));

        ShowMenu(menu, target);
    }

    /// <summary>把当前选择合并成启动器文件夹；先让用户命名。</summary>
    private Task MergeSelectionAsync(IReadOnlyList<LauncherEntry> selected)
    {
        if (selected.Count < 2)
            return Task.CompletedTask;

        var name = PromptDialog.Show(this, "文件夹名称", "新建文件夹");
        if (string.IsNullOrWhiteSpace(name))
            return Task.CompletedTask;

        ViewModel.MergeIntoFolder(selected, selected[0], name);
        return Task.CompletedTask;
    }

    private void OnMergeSelectionMenu(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedEntries;
        if (selected.Count < 2)
            return;
        _ = RunDialogAsync(() => MergeSelectionAsync(selected));
    }

    /// <summary>空白处右键菜单里只在有 2 项以上普通卡片被选中时显示「合并为文件夹」。</summary>
    private void UpdateMergeSelectionMenu()
    {
        var selected = ViewModel.SelectedEntries;
        var canMerge = ViewModel.CanCreateGroup
            && selected.Count >= 2
            && selected.All(entry => !entry.IsGroup);
        MergeSelectionMenu.Visibility = canMerge ? Visibility.Visible : Visibility.Collapsed;
        MergeSelectionMenu.Header = $"合并选中的 {selected.Count} 项为文件夹…";
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
                if (_marqueePressed || _dragActive)
                {
                    CancelMarquee();
                    return;
                }
                // 依次退出：文件夹内 → 清选择 → 清搜索 → 关闭
                if (ViewModel.IsInsideGroup)
                    ViewModel.CloseGroup();
                else if (ViewModel.HasSelection)
                    ViewModel.ClearSelection();
                else if (!string.IsNullOrEmpty(ViewModel.Query))
                    ViewModel.Query = string.Empty;
                else
                    HideLauncher();
                break;

            case Key.A when Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                            && ViewModel.IsEditableView
                            && !SearchBox.IsKeyboardFocusWithin:
                e.Handled = true;
                ViewModel.SetSelection(ViewModel.VisibleEntries);
                break;

            case Key.Delete when ViewModel.HasSelection && ViewModel.IsEditableView:
                e.Handled = true;
                RunDialog(() => ViewModel.RemoveSelected(this));
                break;

            case Key.F5:
                e.Handled = true;
                ViewModel.Reload(forceStartMenuRefresh: true);
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
                SwitchPage(e.Key == Key.Left ? -1 : 1);
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

    /// <summary>
    /// Ctrl + 滚轮切换页签：上滚上一页、下滚下一页，与 <c>←</c> / <c>→</c> 一致。
    /// 只监听 Preview（隧道路由），滚轮在 ScrollViewer 上会被它标记为已处理，冒泡阶段收不到。
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || e.Delta == 0)
            return;
        // 搜索期间页签隐藏、结果是跨页签合并的，与左右键一样不切页
        if (ViewModel.IsSearching)
            return;

        e.Handled = true;
        SwitchPage(e.Delta > 0 ? -1 : 1);
    }

    /// <summary>循环切换页签；delta 为 -1 上一页、1 下一页。</summary>
    private void SwitchPage(int delta)
    {
        var pageCount = ViewModel.Pages.Count;
        ViewModel.SelectedPage = ((ViewModel.SelectedPage + delta) % pageCount + pageCount) % pageCount;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShortcutLauncherViewModel.SelectedPage):
                OnPageSwitched();
                break;
            case nameof(ShortcutLauncherViewModel.IsSearching):
                // 搜索结果是跨页签的另一份列表，保留上一页的滚动位置会出现在半截
                ResetScrollPosition();
                break;
            case nameof(ShortcutLauncherViewModel.IsInsideGroup):
                if (ViewModel.IsInsideGroup)
                    PlayFolderOpenAnimation();
                break;
        }
    }

    /// <summary>
    /// 换页后把网格滚回顶部，并让新页内容从切换方向滑入淡入。
    /// 不重置滚动位置时，在长页（本地应用）滚到底再切到短页（内置工具）会停在旧偏移上，
    /// 工具页的卡片就显示在中部而不是顶部。
    /// </summary>
    private void OnPageSwitched()
    {
        var pageCount = Math.Max(1, ViewModel.Pages.Count);
        var previous = _lastPageIndex;
        var current = ViewModel.SelectedPage;
        _lastPageIndex = current;

        ResetScrollPosition();

        // 预热 / 收起过程中换页不播放动画，否则首次显示会带着一半透明
        if (!IsVisible || _prewarming || ViewModel.IsSearching)
            return;

        // 循环页签按更短的一侧判断方向：4 → 0 是下一页，0 → 4 是上一页
        var forward = ((current - previous) % pageCount + pageCount) % pageCount;
        var backward = ((previous - current) % pageCount + pageCount) % pageCount;

        // 换页时新生成的卡片容器在 Loaded 里做错峰入场；搜索重建容器时不参与
        _entranceAtUtc = DateTime.UtcNow;
        PlayPageTransition(forward <= backward ? 1 : -1);
    }

    private void ResetScrollPosition()
    {
        if (EntryScroller.VerticalOffset > 0)
            EntryScroller.ScrollToTop();
        if (SectionScroller.VerticalOffset > 0)
            SectionScroller.ScrollToTop();
    }

    /// <summary>新页内容从切换方向的一侧（24 DIP）滑入并淡入；只动渲染层，不触发重新布局。</summary>
    private void PlayPageTransition(int direction)
    {
        _pageSlide.BeginAnimation(TranslateTransform.XProperty, null);
        PageContentHost.BeginAnimation(OpacityProperty, null);

        _pageSlide.X = direction * 24;
        PageContentHost.Opacity = 0;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _pageSlide.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(0d, new Duration(TimeSpan.FromMilliseconds(190))) { EasingFunction = ease });
        PageContentHost.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1d, new Duration(TimeSpan.FromMilliseconds(150))) { EasingFunction = ease });
    }

    /// <summary>
    /// 换页时新生成的卡片容器错峰入场：按索引每张晚 9ms，从下方 10 DIP 淡入上浮。
    /// 只在换页后的 500ms 内生效，搜索 / 拖拽等普通刷新重建的容器保持立即可见。
    /// </summary>
    private void OnEntryContainerLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ContentPresenter container || !IsVisible || _prewarming)
            return;

        var elapsed = (DateTime.UtcNow - _entranceAtUtc).TotalMilliseconds;
        if (elapsed < 0 || elapsed > 500)
            return;

        var index = EntryGrid.ItemContainerGenerator.IndexFromContainer(container);
        if (index >= 0)
            PlayTileEntrance(container, index);
    }

    private static void PlayTileEntrance(ContentPresenter container, int index)
    {
        var lift = new TranslateTransform(0, 10);
        container.RenderTransform = lift;
        container.Opacity = 0;

        // 延迟按索引递增，最多 24 张参与错峰；大页末尾不会等太久
        var delay = TimeSpan.FromMilliseconds(Math.Min(index, 24) * 9);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        container.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1d, new Duration(TimeSpan.FromMilliseconds(180)))
            {
                BeginTime = delay,
                EasingFunction = ease,
            });
        lift.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(0d, new Duration(TimeSpan.FromMilliseconds(200)))
            {
                BeginTime = delay,
                EasingFunction = ease,
            });
    }

    /// <summary>文件夹浮层弹出：暗底淡入，卡片从 90% 回弹到 100%。</summary>
    private void PlayFolderOpenAnimation()
    {
        if (!IsVisible || _prewarming)
            return;

        _folderScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _folderScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        FolderCard.BeginAnimation(OpacityProperty, null);
        GroupDim.BeginAnimation(OpacityProperty, null);

        _folderScale.ScaleX = 0.9;
        _folderScale.ScaleY = 0.9;
        FolderCard.Opacity = 0;
        GroupDim.Opacity = 0;

        var back = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
        DoubleAnimation ScaleTo(double target, int milliseconds, IEasingFunction? easing) =>
            new(target, new Duration(TimeSpan.FromMilliseconds(milliseconds))) { EasingFunction = easing };

        _folderScale.BeginAnimation(ScaleTransform.ScaleXProperty, ScaleTo(1d, 260, back));
        _folderScale.BeginAnimation(ScaleTransform.ScaleYProperty, ScaleTo(1d, 260, back));
        FolderCard.BeginAnimation(OpacityProperty, ScaleTo(1d, 150, null));
        GroupDim.BeginAnimation(OpacityProperty, ScaleTo(1d, 160, null));
    }

    /// <summary>
    /// 空白处按下不再关闭窗口（管理图标时点一下空白就消失很碍事），改为框选起点；
    /// 抬起时若没有真正拖动，只清除选择。图标、搜索框、页签与滚动条不算空白。
    /// </summary>
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

        // 文件夹浮层没有返回按钮：点卡片外的空白处直接收起，回到原来的页签
        if (ViewModel.IsInsideGroup && !IsInside(source, FolderCard))
        {
            ViewModel.CloseGroup();
            FocusSearch();
            e.Handled = true;
            return;
        }

        _entryPress = null;
        if (!ViewModel.IsEditableView)
            return;

        _marqueePressed = true;
        _marqueeActive = false;
        _marqueeCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        _marqueeStart = e.GetPosition(SelectionLayer);
        _marqueeSnapshot = ViewModel.SelectedEntries.ToList();
        Root.CaptureMouse();
    }

    /// <summary>空白处拖动超过阈值后绘制选框；移动过程中实时选中与矩形相交的卡片。</summary>
    private void OnRootPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_marqueePressed || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(SelectionLayer);
        if (!_marqueeActive)
        {
            if (Math.Abs(current.X - _marqueeStart.X) < 4
                && Math.Abs(current.Y - _marqueeStart.Y) < 4)
            {
                return;
            }

            _marqueeActive = true;
            if (!_marqueeCtrl)
                ViewModel.ClearSelection();
            SelectionRect.Visibility = Visibility.Visible;
        }

        UpdateMarqueeRect(current);
        SelectEntriesInMarquee(CurrentMarqueeRect(), additive: _marqueeCtrl);
    }

    private void OnRootPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_marqueePressed)
            return;

        var wasActive = _marqueeActive;
        var additive = _marqueeCtrl;
        _marqueePressed = false;
        _marqueeActive = false;
        Root.ReleaseMouseCapture();
        SelectionRect.Visibility = Visibility.Collapsed;

        if (!wasActive && !additive)
            ViewModel.ClearSelection();
        else if (wasActive)
            SelectEntriesInMarquee(CurrentMarqueeRect(), additive);
    }

    private void CancelMarquee()
    {
        var wasActive = _marqueeActive;
        _marqueePressed = false;
        _marqueeActive = false;
        Root.ReleaseMouseCapture();
        SelectionRect.Visibility = Visibility.Collapsed;

        // Esc 取消框选时恢复拖动前的选择
        if (wasActive)
        {
            ViewModel.ClearSelection();
            ViewModel.SetSelection(_marqueeSnapshot);
        }
    }

    private void UpdateMarqueeRect(Point current)
    {
        var rect = MarqueeRect(_marqueeStart, current);
        Canvas.SetLeft(SelectionRect, rect.X);
        Canvas.SetTop(SelectionRect, rect.Y);
        SelectionRect.Width = rect.Width;
        SelectionRect.Height = rect.Height;
    }

    private Rect CurrentMarqueeRect() => MarqueeRect(_marqueeStart, Mouse.GetPosition(SelectionLayer));

    private static Rect MarqueeRect(Point start, Point end) => new(
        Math.Min(start.X, end.X),
        Math.Min(start.Y, end.Y),
        Math.Abs(end.X - start.X),
        Math.Abs(end.Y - start.Y));

    /// <summary>选中所有与框选矩形相交的卡片；附加模式下保留原有选择。</summary>
    private void SelectEntriesInMarquee(Rect rect, bool additive)
    {
        if (rect.Width < 2 && rect.Height < 2)
            return;

        var hits = new List<LauncherEntry>();
        for (var i = 0; i < ViewModel.VisibleEntries.Count; i++)
        {
            if (EntryContainer(i) is not { } container)
                continue;
            if (rect.IntersectsWith(GetBounds(container)))
                hits.Add(ViewModel.VisibleEntries[i]);
        }

        var selection = additive
            ? ViewModel.SelectedEntries.Concat(hits).Distinct().ToList()
            : hits;
        ViewModel.SetSelection(selection);
    }

    /// <summary>当前可交互的网格：文件夹浮层打开时对浮层里的卡片做框选 / 拖拽，否则对页签网格。</summary>
    private ItemsControl ActiveGrid => ViewModel.IsInsideGroup ? GroupGrid : EntryGrid;

    private FrameworkElement? EntryContainer(int index) =>
        ActiveGrid.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;

    /// <summary>把卡片容器换算到覆盖层坐标；滚动位置由 TranslatePoint 自动计入。</summary>
    private Rect GetBounds(FrameworkElement container)
    {
        try
        {
            var origin = container.TranslatePoint(new Point(0, 0), SelectionLayer);
            return new Rect(origin, new Size(container.ActualWidth, container.ActualHeight));
        }
        catch (InvalidOperationException)
        {
            return Rect.Empty;
        }
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
        if (_suppressDeactivate || _prewarming || _menuOpen || _dragActive || _marqueeActive || !IsVisible)
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
    /// 启动器悬浮且置顶时，没有 owner 的系统文件对话框会被它挡住。
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

    private void OnReloadMenu(object sender, RoutedEventArgs e) =>
        ViewModel.Reload(forceStartMenuRefresh: true);

    private void OnCloseMenu(object sender, RoutedEventArgs e) => HideLauncher();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(EntryDragFormat) == true)
        {
            if (!ViewModel.IsEditableView)
            {
                _dropPlan = null;
                HideDropIndicators();
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            _dropPlan = ResolveDropPlan(e);
            ShowDropIndicators(_dropPlan);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        _dropPlan = null;
        HideDropIndicators();
        e.Effects = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            && ViewModel.CanAcceptExternalDrop
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        HideDropIndicators();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(EntryDragFormat) is IReadOnlyList<LauncherEntry> entries && entries.Count > 0)
        {
            var plan = _dropPlan ?? ResolveDropPlan(e);
            _dropPlan = null;
            HideDropIndicators();

            if (plan is not null)
            {
                if (plan.Mode == DropMode.Merge && plan.Target is not null)
                {
                    var target = plan.Target;
                    RunDialog(() => ViewModel.MergeIntoFolder(entries, target));
                }
                else
                {
                    var insertIndex = plan.InsertIndex;
                    RunDialog(() => ViewModel.MoveTiles(entries, insertIndex));
                }
            }

            e.Handled = true;
            return;
        }

        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;
        if (!ViewModel.CanAcceptExternalDrop)
        {
            e.Handled = true;
            return;
        }

        foreach (var file in files)
        {
            if (!string.IsNullOrWhiteSpace(file))
                ViewModel.AddDropped(file);
        }

        FocusSearch();
        e.Handled = true;
    }

    /// <summary>根据指针位置计算落点：图标中心 = 合并 / 加入组，其余位置 = 插入排序。</summary>
    private DropPlan? ResolveDropPlan(DragEventArgs e)
    {
        if (e.Data?.GetData(EntryDragFormat) is not IReadOnlyList<LauncherEntry> entries
            || entries.Count == 0)
        {
            return null;
        }

        var target = FindEntry(e.OriginalSource as DependencyObject);
        if (target is null)
            return new DropPlan(DropMode.Insert, null, ViewModel.VisibleEntries.Count, null);

        var targetIndex = ViewModel.VisibleEntries.IndexOf(target);
        if (targetIndex < 0 || EntryContainer(targetIndex) is not { } container)
            return new DropPlan(DropMode.Insert, null, ViewModel.VisibleEntries.Count, null);

        var bounds = GetBounds(container);
        var position = e.GetPosition(container);

        // 卡片 90×112，图标 48×48 居中；热区略放大便于拖放合并
        var iconZone = new Rect(16, 10, 58, 56);
        var canMerge = !ViewModel.IsInsideGroup
            && target.Kind is not (ShortcutKind.Tool or ShortcutKind.InternalTool)
            && entries.All(entry => !entry.IsGroup)
            && !entries.Contains(target)
            && (target.IsGroup || ViewModel.CanCreateGroup)
            && iconZone.Contains(position);

        if (canMerge)
            return new DropPlan(DropMode.Merge, target, targetIndex, bounds);

        var insertIndex = targetIndex + (position.X > container.ActualWidth / 2 ? 1 : 0);
        return new DropPlan(DropMode.Insert, target, insertIndex, bounds);
    }

    private static LauncherEntry? FindEntry(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: LauncherEntry entry })
                return entry;
            source = ParentOf(source);
        }
        return null;
    }

    private void ShowDropIndicators(DropPlan? plan)
    {
        HideDropIndicators();
        if (plan is null)
            return;

        if (plan.Mode == DropMode.Merge && plan.TargetRect is { } targetRect)
        {
            Canvas.SetLeft(DropHighlight, targetRect.X + 28);
            Canvas.SetTop(DropHighlight, targetRect.Y + 10);
            DropHighlight.Width = 76;
            DropHighlight.Height = 76;
            DropHighlight.Visibility = Visibility.Visible;
            return;
        }

        double x;
        double y;
        var count = ViewModel.VisibleEntries.Count;
        if (plan.InsertIndex < count && EntryContainer(plan.InsertIndex) is { } next)
        {
            var rect = GetBounds(next);
            x = rect.X - 2;
            y = rect.Y + 20;
        }
        else if (count > 0 && EntryContainer(count - 1) is { } last)
        {
            var rect = GetBounds(last);
            x = rect.Right + 2;
            y = rect.Y + 20;
        }
        else
        {
            x = SelectionLayer.ActualWidth / 2;
            y = SelectionLayer.ActualHeight / 2 - 55;
        }

        Canvas.SetLeft(InsertionBar, x);
        Canvas.SetTop(InsertionBar, y);
        InsertionBar.Visibility = Visibility.Visible;
    }

    private void HideDropIndicators()
    {
        DropHighlight.Visibility = Visibility.Collapsed;
        InsertionBar.Visibility = Visibility.Collapsed;
    }

    #endregion
}
