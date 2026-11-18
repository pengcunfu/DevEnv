using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using DevEnv.Host.ViewModels;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Views;

/// <summary>
/// 独立分类窗口（本地应用 / 文件夹 / 网站 / 开始菜单）的内容视图。
/// 与启动器共用同一套图标卡片样式与交互（框选、拖动排序、合并文件夹、右键菜单），
/// 但不再有页签切换与失焦收起：窗口就是普通工具窗，关闭走标题栏按钮。
/// </summary>
public partial class ShortcutCategoryView : UserControl
{
    private static readonly Uri LauncherLightPalette =
        new("/DevEnv;component/Themes/Launcher.Light.xaml", UriKind.Relative);

    private static readonly Uri LauncherDarkPalette =
        new("/DevEnv;component/Themes/Launcher.Dark.xaml", UriKind.Relative);

    /// <summary>启动器内部拖拽的数据格式；不与外部文件拖入的 FileDrop 混用。</summary>
    private const string EntryDragFormat = "DevEnv.LauncherEntries";

    /// <summary>按下后移动多少 DIP 才视为拖拽；管理操作用更保守的阈值。</summary>
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

    private readonly ScaleTransform _folderScale = new(1, 1);

    /// <summary>由 ToolWindowHost 创建：页签索引 0 本地应用 / 1 文件夹 / 2 网站 / 5 开始菜单。</summary>
    public ShortcutCategoryView(int page)
    {
        InitializeComponent();
        DataContext = new ShortcutLauncherViewModel(page);
        ApplySystemColorScheme();

        AllowDrop = true;
        DragOver += OnDragOver;
        DragLeave += OnDragLeave;
        Drop += OnDrop;

        FolderCard.RenderTransform = _folderScale;
        FolderCard.RenderTransformOrigin = new Point(0.5, 0.5);

        // 模板来自共用 ResourceDictionary，事件统一挂在网格上靠冒泡处理
        foreach (var grid in new[] { EntryGrid, GroupGrid })
        {
            grid.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnEntryClick));
            grid.AddHandler(PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnEntryPreviewMouseLeftButtonDown));
            grid.AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(OnEntryPreviewMouseMove));
            grid.AddHandler(ContextMenuService.ContextMenuOpeningEvent,
                new ContextMenuEventHandler(OnEntryContextMenuOpening));
        }

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShortcutLauncherViewModel.IsInsideGroup)
                && ViewModel.IsInsideGroup)
            {
                PlayFolderOpenAnimation();
            }
        };
    }

    public ShortcutLauncherViewModel ViewModel => (ShortcutLauncherViewModel)DataContext;

    /// <summary>启动器配色跟随 Windows「应用模式」；两套调色板 key 完全一致。</summary>
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

    private void OnLoaded(object sender, RoutedEventArgs e) => FocusSearch();

    private void FocusSearch()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    /// <summary>对话框 owner：视图一定挂在 ToolWindow 里，取不到时退回当前激活窗口。</summary>
    private Window OwnerWindow =>
        (Window.GetWindow(this) ?? Application.Current?.MainWindow)!;

    #region 交互

    /// <summary>图标单击启动；有多选时第一次点击只收窄选择，避免管理图标时误启动。</summary>
    private void OnEntryClick(object sender, RoutedEventArgs e)
    {
        if (FindEntry(e.OriginalSource as DependencyObject) is not { } entry)
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
        if (FindEntry(e.OriginalSource as DependencyObject) is not { } entry)
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

        // 鼠标捕获在卡片按钮上，不在模板深处的可视元素上
        var dragSource = FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) as UIElement ?? this;
        dragSource.ReleaseMouseCapture();

        _dragActive = true;
        try
        {
            DragDrop.DoDragDrop(
                (DependencyObject)dragSource,
                new DataObject(EntryDragFormat, entries),
                DragDropEffects.Move);
        }
        finally
        {
            _dragActive = false;
            HideDropIndicators();
            _suppressEntryClickUntilUtc = DateTime.UtcNow.AddMilliseconds(300);
        }
    }

    private void LaunchEntry(LauncherEntry entry)
    {
        ViewModel.Message = string.Empty;

        // 文件夹磁贴只展开内容，不启动
        if (entry.IsGroup)
        {
            ViewModel.OpenGroup(entry);
            FocusSearch();
            return;
        }

        ViewModel.Launch(entry);
    }

    private void OnEntryContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (FindEntry(e.OriginalSource as DependencyObject) is not { } entry)
            return;

        // 右键落在多选项上：先保持选择，显示批量菜单
        if (ViewModel.HasSelection && entry.IsSelected && ViewModel.SelectedEntries.Count > 1)
        {
            ShowBatchMenu(e.OriginalSource as FrameworkElement);
            e.Handled = true;
            return;
        }

        var menu = new ContextMenu();

        // 开始菜单条目只读：可启动、收进启动器、定位文件
        if (entry.Kind == ShortcutKind.StartMenu)
        {
            menu.Items.Add(MenuItemFor("运行", () => LaunchEntry(entry)));
            menu.Items.Add(MenuItemFor("添加到启动器", () => ViewModel.AddStartMenuEntry(entry)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor("在资源管理器中显示", () => ViewModel.Reveal(entry)));
            ShowMenu(menu, e.OriginalSource as FrameworkElement);
            e.Handled = true;
            return;
        }

        // 开始菜单按一级文件夹分出来的只读文件夹只提供展开
        if (entry.IsGroup && entry.IsReadOnlyGroup)
        {
            menu.Items.Add(MenuItemFor("打开文件夹", () => ViewModel.OpenGroup(entry)));
            ShowMenu(menu, e.OriginalSource as FrameworkElement);
            e.Handled = true;
            return;
        }

        if (entry.IsGroup)
        {
            menu.Items.Add(MenuItemFor("打开文件夹", () => ViewModel.OpenGroup(entry)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor("重命名文件夹",
                () => _ = RunDialogAsync(() => ViewModel.RenameAsync(entry, OwnerWindow))));
            menu.Items.Add(MenuItemFor("解散文件夹", () => RunDialog(() => ViewModel.DissolveGroup(entry))));
            ShowMenu(menu, e.OriginalSource as FrameworkElement);
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
                () => ViewModel.Reveal(entry)));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor("编辑别名与图标",
            () => _ = RunDialogAsync(() => ViewModel.RenameAsync(entry, OwnerWindow))));
        menu.Items.Add(MenuItemFor("刷新图标",
            () => _ = RunDialogAsync(() => ViewModel.RefreshIconAsync(entry))));

        if (ViewModel.IsInsideGroup)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor("移出文件夹", () => RunDialog(() => ViewModel.MoveOutOfGroup([entry]))));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor("移除", () => RunDialog(() => ViewModel.Remove(entry, OwnerWindow))));

        ShowMenu(menu, e.OriginalSource as FrameworkElement);
        e.Handled = true;
    }

    /// <summary>多选后的批量菜单：合并为文件夹、移出文件夹、批量移除、取消选择。</summary>
    private void ShowBatchMenu(FrameworkElement? target)
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
            () => RunDialog(() => ViewModel.RemoveSelected(OwnerWindow))));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor("取消选择", ViewModel.ClearSelection));

        ShowMenu(menu, target);
    }

    private Task MergeSelectionAsync(IReadOnlyList<LauncherEntry> selected)
    {
        if (selected.Count < 2 || OwnerWindow is not { } owner)
            return Task.CompletedTask;

        var name = PromptDialog.Show(owner, "文件夹名称", "新建文件夹");
        if (string.IsNullOrWhiteSpace(name))
            return Task.CompletedTask;

        ViewModel.MergeIntoFolder(selected, selected[0], name);
        return Task.CompletedTask;
    }

    private void OnRootMenuOpened(object sender, RoutedEventArgs e)
    {
        AddAppMenu.Visibility = ViewModel.CanAddApps ? Visibility.Visible : Visibility.Collapsed;
        AddFolderMenu.Visibility = ViewModel.CanAddFolders ? Visibility.Visible : Visibility.Collapsed;
        AddWebMenu.Visibility = ViewModel.CanAddWebs ? Visibility.Visible : Visibility.Collapsed;
        AddSeparator.Visibility = ViewModel.CanAddApps || ViewModel.CanAddFolders || ViewModel.CanAddWebs
            ? Visibility.Visible
            : Visibility.Collapsed;

        var selected = ViewModel.SelectedEntries;
        var canMerge = ViewModel.CanCreateGroup
            && selected.Count >= 2
            && selected.All(entry => !entry.IsGroup);
        MergeSelectionMenu.Visibility = canMerge ? Visibility.Visible : Visibility.Collapsed;
        MergeSelectionMenu.Header = $"合并选中的 {selected.Count} 项为文件夹…";
        MergeSeparator.Visibility = canMerge ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMergeSelectionMenu(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedEntries;
        if (selected.Count < 2)
            return;
        _ = RunDialogAsync(() => MergeSelectionAsync(selected));
    }

    private static MenuItem MenuItemFor(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static void ShowMenu(ContextMenu menu, FrameworkElement? target)
    {
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
                // 依次退出：文件夹内 → 清选择 → 清搜索；窗口本身不因 Esc 关闭
                if (ViewModel.IsInsideGroup)
                    ViewModel.CloseGroup();
                else if (ViewModel.HasSelection)
                    ViewModel.ClearSelection();
                else if (!string.IsNullOrEmpty(ViewModel.Query))
                    ViewModel.Query = string.Empty;
                break;

            case Key.A when Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                            && ViewModel.IsEditableView
                            && !SearchBox.IsKeyboardFocusWithin:
                e.Handled = true;
                ViewModel.SetSelection(ViewModel.VisibleEntries);
                break;

            case Key.Delete when ViewModel.HasSelection
                                 && ViewModel.IsEditableView
                                 && !SearchBox.IsKeyboardFocusWithin:
                e.Handled = true;
                RunDialog(() => ViewModel.RemoveSelected(OwnerWindow));
                break;

            case Key.F5:
                e.Handled = true;
                ViewModel.Reload(forceStartMenuRefresh: true);
                break;

            case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                e.Handled = true;
                FocusSearch();
                break;

            case Key.Enter:
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

    /// <summary>文件夹浮层弹出：暗底淡入，卡片从 90% 回弹到 100%。</summary>
    private void PlayFolderOpenAnimation()
    {
        if (!IsVisible)
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

    /// <summary>空白处按下改为框选起点；图标、搜索框与滚动条不算空白。</summary>
    private void OnRootPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;
        if (FindAncestor<ButtonBase>(source) is not null
            || FindAncestor<TextBoxBase>(source) is not null
            || FindAncestor<ScrollBar>(source) is not null
            || IsInside(source, SearchShell))
            return;

        // 文件夹浮层没有返回按钮：点卡片外的空白处直接收起
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

    /// <summary>当前可交互的网格：文件夹浮层打开时对浮层里的卡片操作，否则对主网格。</summary>
    private ItemsControl ActiveGrid => ViewModel.IsInsideGroup ? GroupGrid : EntryGrid;

    private FrameworkElement? EntryContainer(int index) =>
        ActiveGrid.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;

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

    private async Task RunDialogAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ViewModel.Message = ex.Message;
        }
    }

    private void RunDialog(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ViewModel.Message = ex.Message;
        }
    }

    #endregion

    #region 添加与拖放

    private void OnAddClick(object sender, RoutedEventArgs e) =>
        _ = RunDialogAsync(() => ViewModel.AddForCurrentPageAsync(OwnerWindow));

    private void OnAddAppMenu(object sender, RoutedEventArgs e) => RunDialog(ViewModel.AddApp);

    private void OnAddFolderMenu(object sender, RoutedEventArgs e) => RunDialog(ViewModel.AddFolder);

    private void OnAddWebMenu(object sender, RoutedEventArgs e) =>
        _ = RunDialogAsync(() => ViewModel.AddWebAsync(OwnerWindow));

    private void OnReloadMenu(object sender, RoutedEventArgs e) =>
        ViewModel.Reload(forceStartMenuRefresh: true);

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

    private void OnDragLeave(object sender, DragEventArgs e) => HideDropIndicators();

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
            && entries.All(entry => !entry.IsGroup)
            && !entries.Contains(target)
            && (target.IsGroup || ViewModel.CanCreateGroup)
            && iconZone.Contains(position);

        if (canMerge)
            return new DropPlan(DropMode.Merge, target, targetIndex, bounds);

        var insertIndex = targetIndex + (position.X > container.ActualWidth / 2 ? 1 : 0);
        return new DropPlan(DropMode.Insert, target, insertIndex, bounds);
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
