using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DevEnv.Host.SigXor.Diagnostics;
using DevEnv.Host.ViewModels;
using DevEnv.Host.Views;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 启动器自检：启动器本身只保留系统工具 / 内置工具两页，
/// 本地应用 / 文件夹 / 网站 / 开始菜单已拆成独立窗口。
/// 探针同时验证启动器的系统工具菜单栏与内置工具分组，以及四个分类窗口的加载、
/// 只读判定、选择 API、搜索范围与文件夹浮层。
/// 用法：<c>DevEnv.exe --shortcuts-check %TEMP%\shortcuts.txt</c>
/// </summary>
public static class ShortcutLauncherProbe
{
    public const string Argument = "--shortcuts-check";

    private static readonly (int Page, string Title)[] CategoryPages =
    [
        (0, "本地应用"),
        (1, "文件夹"),
        (2, "网站"),
        (5, "开始菜单"),
    ];

    public static int Run(string[] args)
    {
        var report = new StringBuilder();
        var bindingErrors = new List<string>();
        var failures = 0;

        void Check(string name, bool ok, string? detail = null)
        {
            if (ok)
            {
                report.AppendLine($"OK    {name}");
            }
            else
            {
                failures++;
                report.AppendLine($"FAIL  {name}{(detail is null ? "" : ": " + detail)}");
            }
        }

        var listener = new BindingErrorListener(bindingErrors);
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

        ShortcutsWindow? window = null;
        var categoryWindows = new List<Window>();
        try
        {
            window = new ShortcutsWindow
            {
                ShowInTaskbar = false,
                ShowActivated = false,
            };

            // 预热：以最终几何、全透明、不激活的状态显示一次再隐藏
            window.Prewarm();
            Pump();
            Check("预热后保持隐藏（不打扰当前前台程序）", !window.IsVisible);
            Check("预热已完成", window.IsPrewarmed);
            var expected = ShortcutsWindow.ComputeScreenBounds();
            Check("预热使用最终几何（默认 960 × 680 DIP、居中，显示时不再移动窗口）",
                Math.Abs(window.Left - expected.Left) < 0.5
                && Math.Abs(window.Top - expected.Top) < 0.5
                && Math.Abs(window.Width - expected.Width) < 0.5
                && Math.Abs(window.Height - expected.Height) < 0.5,
                $"Left={window.Left}, Top={window.Top}, W={window.Width}, H={window.Height}, " +
                $"expected=({expected.Left},{expected.Top},{expected.Width},{expected.Height})");
            var area = CursorScreen.GetWorkAreaDip();
            Check("启动器默认尺寸为 960 × 680 DIP",
                Math.Abs(window.Width - DevEnv.Core.Services.LauncherGeometry.Width) < 0.5
                && Math.Abs(window.Height - DevEnv.Core.Services.LauncherGeometry.Height) < 0.5,
                $"W={window.Width}, H={window.Height}, area={area.Width}×{area.Height}");
            var leftGap = window.Left - area.Left;
            var rightGap = area.Left + area.Width - (window.Left + window.Width);
            var topGap = window.Top - area.Top;
            var bottomGap = area.Top + area.Height - (window.Top + window.Height);
            Check("启动器在鼠标所在屏工作区内水平、垂直居中",
                Math.Abs(leftGap - rightGap) <= 1 && Math.Abs(topGap - bottomGap) <= 1,
                $"left={leftGap:F1}, right={rightGap:F1}, top={topGap:F1}, bottom={bottomGap:F1}");
            Check("启动器配色已加载（跟随系统浅 / 深色）",
                window.TryFindResource("LauncherBackgroundBrush") is System.Windows.Media.Brush
                && window.TryFindResource("LauncherTextPrimaryBrush") is System.Windows.Media.Brush
                && window.Background is System.Windows.Media.Brush,
                $"window.Background={window.Background?.GetType().Name ?? "null"}");

            // 后面的渲染检查放到屏幕外，避免真的盖住用户桌面
            window.Left = -4000;
            window.Top = -4000;
            window.Show();
            Pump();
            window.UpdateLayout();

            Check("启动器窗口可正常加载", window.IsVisible);
            Check("窗口标题为「燧岚开发盒子」", window.Title == "燧岚开发盒子", $"Title={window.Title}");
            Check("启动器不挂 Owner（不会带出主窗口）", window.Owner is null);
            Check("使用 Windows 默认窗口（标准边框与标题栏）",
                window.WindowStyle == WindowStyle.SingleBorderWindow && !window.AllowsTransparency,
                $"WindowStyle={window.WindowStyle}, AllowsTransparency={window.AllowsTransparency}");
            if (window.DataContext is not ShortcutLauncherViewModel vm)
                throw new InvalidOperationException("DataContext 不是 ShortcutLauncherViewModel");

            // 顶栏只有系统工具菜单，内置工具网格在其下方
            var menuTop = window.SystemToolsMenuItem.TranslatePoint(new Point(0, 0), window).Y;
            var contentTop = window.PageContentHost.TranslatePoint(new Point(0, 0), window).Y;
            Check("系统工具菜单位于窗口顶部", menuTop < 40, $"menuTop={menuTop:F1}");
            Check("内置工具网格位于菜单栏下方", contentTop > menuTop,
                $"contentTop={contentTop:F1}, menuTop={menuTop:F1}");
            Check("启动器菜单栏不抢 Alt 键焦点", !window.MainMenu.IsMainMenu,
                "IsMainMenu 必须为 False，否则单按 Alt 会把焦点移到菜单栏");
            Check("启动器不再提供搜索框",
                VisualTreeSearch.FindDescendant<System.Windows.Controls.TextBox>(window) is null);

            WaitUntil(() => !vm.IsShortcutDataLoading, 15_000);
            Pump();

            Check("启动器只剩内置工具一页（系统工具已进菜单栏）",
                vm.Pages.Count == 1 && vm.Pages[0].Title == "内置工具",
                string.Join(" / ", vm.Pages.Select(p => p.Title)));
            Check("启动器默认显示内置工具页", vm.SelectedPage == 4 && vm.IsBuiltinToolsPage);

            // 系统工具全部挂在顶部菜单栏：分类子菜单 + 叶子工具项
            Check("系统工具全部进入菜单栏",
                vm.SystemToolMenus.Count >= 2
                && vm.SystemToolMenus.All(section =>
                    !string.IsNullOrWhiteSpace(section.Title)
                    && section.Entries.Count > 0
                    && section.Entries.All(entry => entry.Kind == ShortcutKind.Tool)),
                $"menus={vm.SystemToolMenus.Count}, builtin={vm.VisibleEntries.Count}, message={vm.Message}");
            var menuEntries = vm.SystemToolMenus.SelectMany(section => section.Entries).ToList();
            Check("菜单栏覆盖完整的 Windows 工具目录",
                menuEntries.Count > 0
                && menuEntries.Select(entry => entry.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    == menuEntries.Count,
                $"共 {menuEntries.Count} 项");
            Check("菜单栏已绑定系统工具菜单",
                ReferenceEquals(window.SystemToolsMenuItem.ItemsSource, vm.SystemToolMenus),
                "SystemToolsMenuItem.ItemsSource 与 SystemToolMenus 不是同一集合");

            // 菜单必须真的能点开，并且分类 / 叶子项都生成了容器
            window.UpdateLayout();
            Pump();
            window.SystemToolsMenuItem.IsSubmenuOpen = true;
            Pump();
            var groupItem = window.SystemToolsMenuItem.ItemContainerGenerator
                .ContainerFromIndex(0) as MenuItem;
            Check("系统工具菜单可展开并生成分类项",
                groupItem is not null,
                $"IsSubmenuOpen={window.SystemToolsMenuItem.IsSubmenuOpen}");
            if (groupItem is not null)
            {
                groupItem.IsSubmenuOpen = true;
                Pump();
                var leafItem = groupItem.ItemContainerGenerator.ContainerFromIndex(0) as MenuItem;
                Check("系统工具叶子项已生成且带条目数据",
                    leafItem is { DataContext: LauncherEntry },
                    leafItem is null ? "叶子容器为 null" : $"Header={leafItem.Header?.GetType().Name ?? "null"}");
            }

            window.SystemToolsMenuItem.IsSubmenuOpen = false;
            Pump();
            Check("内置工具分类网格已生成容器",
                window.SectionGrid.Items.Count > 0
                && window.SectionGrid.ItemContainerGenerator.ContainerFromIndex(0) is not null,
                $"sections={window.SectionGrid.Items.Count}");

            vm.SelectedPage = 4;
            Pump();
            Check("内置工具页载入宿主工具目录",
                vm.IsBuiltinToolsPage
                && vm.VisibleEntries.Count == ToolWindowHost.ToolIds.Count + 3 // 另含翻译 / 剪贴板 / 文件浏览器
                && vm.VisibleEntries.All(e => e.Kind == ShortcutKind.InternalTool),
                $"共 {vm.VisibleEntries.Count} 项");
            Check("内置工具都带彩色图标",
                vm.VisibleEntries.All(e => e.HasToolBadge && !e.ShowsKindGlyph),
                $"缺图标 {vm.VisibleEntries.Count(e => !e.HasToolBadge)} 项");
            var builtinKeys = vm.VisibleEntries.Select(e => e.Key).ToList();
            Check("内置工具含四个独立窗口入口",
                builtinKeys.Contains("shortcut-apps")
                && builtinKeys.Contains("shortcut-folders")
                && builtinKeys.Contains("shortcut-webs")
                && builtinKeys.Contains("shortcut-startmenu"),
                string.Join(" / ", builtinKeys));
            Check("四个窗口入口归在「窗口入口」分类",
                vm.VisibleEntries
                    .Where(e => e.Key.StartsWith("shortcut-", StringComparison.OrdinalIgnoreCase))
                    .All(e => e.Category == "窗口入口"));
            Check("内置工具页只读（无添加 / 框选 / 分组）",
                !vm.IsEditableView && !vm.CanAddOnCurrentPage && !vm.CanCreateGroup);

            vm.SelectedPage = 4;
            Pump();
            Check("内置工具按分类标签展示",
                vm.ShowsToolSections
                && vm.ToolSections.Count >= 4
                && vm.ToolSections.Any(section => section.Title == "窗口入口"),
                $"sections={string.Join(", ", vm.ToolSections.Select(s => s.Title))}");

            window.ShowPage("tools");
            WaitUntil(() => !vm.IsShortcutDataLoading && vm.SystemToolMenus.Count > 0, 15_000);
            Pump();
            Check("DEVENV_START_PAGE=tools 展开系统工具菜单",
                vm.SelectedPage == 4
                && !vm.IsInsideGroup
                && !vm.IsSearching
                && window.SystemToolsMenuItem.IsSubmenuOpen,
                $"SelectedPage={vm.SelectedPage}, menuOpen={window.SystemToolsMenuItem.IsSubmenuOpen}");
            window.SystemToolsMenuItem.IsSubmenuOpen = false;
            window.ShowPage("builtin");
            WaitUntil(() => !vm.IsShortcutDataLoading, 15_000);
            Pump();
            Check("DEVENV_START_PAGE=builtin 切到内置工具页签",
                vm.SelectedPage == 4 && vm.IsBuiltinToolsPage && !vm.IsInsideGroup && !vm.IsSearching);
            Check("ShowPage 可见时安装窗口外点击监听", window.IsOutsideClickHookInstalled);

            Check("启动器允许最大化与最小化",
                window.ResizeMode == ResizeMode.CanResize,
                $"ResizeMode={window.ResizeMode}");
            window.WindowState = WindowState.Minimized;
            Pump();
            Check("启动器可最小化", window.WindowState == WindowState.Minimized);
            Check("最小化时卸载窗口外点击监听", !window.IsOutsideClickHookInstalled);
            Check("最小化不会触发自动收起", window.IsVisible, $"visible={window.IsVisible}");
            window.WindowState = WindowState.Normal;
            Pump();
            Check("还原后重新安装窗口外点击监听", window.IsOutsideClickHookInstalled);

            window.SimulateOutsideClick();
            Check("窗口外点击立即收起并隐藏", !window.IsVisible);
            Check("窗口外点击后卸载监听", !window.IsOutsideClickHookInstalled);
            window.ShowPage("tools");
            Pump();
            Check("窗口外点击收起后可以重新打开",
                window.IsVisible && window.IsOutsideClickHookInstalled,
                $"visible={window.IsVisible}, hook={window.IsOutsideClickHookInstalled}");

            window.ShowPage("builtin");
            WaitUntil(() => !vm.IsShortcutDataLoading, 15_000);
            Pump();
            window.Dismiss();
            Check("收起立即隐藏（无退出动画）", !window.IsVisible);

            // 托盘 / 左 Alt / 显示主窗口不传 pageId，应留在隐藏前的页签
            window.ShowPage(null);
            WaitUntil(() => !vm.IsShortcutDataLoading, 15_000);
            Pump();
            Check("再次打开仍正常显示", window.IsVisible, $"visible={window.IsVisible}");
            Check("再次打开保留上次页签",
                vm.SelectedPage == 4 && vm.IsBuiltinToolsPage,
                $"SelectedPage={vm.SelectedPage}");

            // 四个分类窗口：各自单页、独立窗口，不再有页签切换
            foreach (var (page, title) in CategoryPages)
            {
                Check($"分类窗口 id「{title}」已注册", ToolWindowHost.IsTool(
                    page switch
                    {
                        0 => "shortcut-apps",
                        1 => "shortcut-folders",
                        2 => "shortcut-webs",
                        _ => "shortcut-startmenu",
                    }));

                var view = new ShortcutCategoryView(page);
                var host = new Window
                {
                    Title = title,
                    Width = 900,
                    Height = 640,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -4000,
                    Top = -4000,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Content = view,
                };
                categoryWindows.Add(host);
                host.Show();
                Pump();
                WaitUntil(() => !view.ViewModel.IsShortcutDataLoading, 15_000);
                if (page == 5)
                    WaitUntil(() => !view.ViewModel.IsStartMenuLoading, 15_000);
                Pump();
                host.UpdateLayout();

                var category = view.ViewModel;
                Check($"分类窗口「{title}」可加载", host.IsVisible && category.Pages.Count == 1);
                Check($"分类窗口「{title}」固定单页",
                    category.SelectedPage == page && category.IsCategoryWindow);

                var expectedKind = page switch
                {
                    0 => ShortcutKind.App,
                    1 => ShortcutKind.Folder,
                    2 => ShortcutKind.Web,
                    _ => ShortcutKind.StartMenu,
                };
                Check($"分类窗口「{title}」只含本类条目",
                    category.VisibleEntries.All(e => e.Kind == expectedKind || e.IsGroup),
                    $"共 {category.VisibleEntries.Count} 项");
                Check($"分类窗口「{title}」可编辑判定正确",
                    page == 5
                        ? !category.IsEditableView && !category.CanCreateGroup && !category.CanAddOnCurrentPage
                        : category.IsEditableView && category.CanCreateGroup && category.CanAddOnCurrentPage);
                Check($"分类窗口「{title}」右键菜单入口正确",
                    page == 0 ? category.CanAddApps && !category.CanAddFolders && !category.CanAddWebs
                    : page == 1 ? category.CanAddFolders && !category.CanAddApps && !category.CanAddWebs
                    : page == 2 ? category.CanAddWebs && !category.CanAddApps && !category.CanAddFolders
                    : !category.CanAddApps && !category.CanAddFolders && !category.CanAddWebs);

                if (category.VisibleEntries.FirstOrDefault() is { } selectable)
                {
                    category.SetSelection([selectable]);
                    Check($"分类窗口「{title}」选中卡片后记录选择",
                        category.HasSelection && category.SelectedEntries.Count == 1);
                    category.ClearSelection();
                    Check($"分类窗口「{title}」清空选择", !category.HasSelection);
                }

                if (page == 0 && category.AddDiagnosticGroup())
                {
                    host.UpdateLayout();
                    Pump();
                    var group = category.VisibleEntries.FirstOrDefault(entry => entry.IsGroup);
                    Check("分类窗口内文件夹磁贴可渲染（含 2×2 预览）", group is not null);
                    if (group is not null)
                    {
                        category.OpenGroup(group);
                        Pump();
                        Check("打开文件夹后显示组内条目",
                            category.IsInsideGroup && category.VisibleEntries.Count == group.GroupItems.Count);
                        Check("文件夹浮层打开时底层卡片保留",
                            category.PageEntries.Count > 0 && category.PageEntries.Any(entry => entry.IsGroup));
                        Check("文件夹内禁止再建文件夹", !category.CanCreateGroup);
                        category.CloseGroup();
                        Pump();
                        Check("关闭文件夹后回到页签视图", !category.IsInsideGroup);
                    }

                    category.RemoveDiagnosticGroup();
                    Pump();
                }

                var sample = category.VisibleEntries.FirstOrDefault(entry => !entry.IsGroup);
                if (sample is not null)
                {
                    var needle = sample.Name.Length > 2 ? sample.Name[..2] : sample.Name;
                    category.Query = needle;
                    Pump();
                    Check($"分类窗口「{title}」搜索「{needle}」只命中本类",
                        category.IsSearching
                        && category.VisibleEntries.Any(e => e.Name == sample.Name)
                        && category.VisibleEntries.All(e => e.Kind == expectedKind));
                    category.Query = string.Empty;
                    Pump();
                }

                host.Close();
            }

            Check("无数据绑定错误", bindingErrors.Count == 0,
                bindingErrors.Count == 0 ? null : string.Join(" | ", bindingErrors.Take(5)));
        }
        catch (Exception ex)
        {
            failures++;
            report.AppendLine($"FAIL  探针异常: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            foreach (var categoryWindow in categoryWindows)
            {
                try { categoryWindow.Close(); } catch { /* 探针退出路径 */ }
            }
            try { window?.Close(); } catch { /* 探针退出路径 */ }
        }

        var text = report.ToString();
        ProbeOutput.Line(text);
        ProbeOutput.Line($"失败 {failures} 项 / 共 {failures + text.Split('\n').Count(l => l.StartsWith("OK ", StringComparison.Ordinal))} 项");

        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            try { File.WriteAllText(args[0], ProbeOutput.Flush()); } catch { /* 报告写失败不影响退出码 */ }
        }

        return failures == 0 ? 0 : 1;
    }

    /// <summary>反复跑消息循环直到条件成立（最多等 timeoutMs）。</summary>
    private static void WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            Pump();
            Thread.Sleep(10);
        }
    }

    /// <summary>跑一轮消息循环，让布局、绑定与 Loaded 事件落定。</summary>
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    /// <summary>收集 WPF 数据绑定的错误输出。</summary>
    private sealed class BindingErrorListener(List<string> sink) : TraceListener
    {
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                sink.Add(message);
        }
    }
}
