using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DevEnv.Host.SigXor.Diagnostics;
using DevEnv.Host.ViewModels;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 启动器自检：验证主窗口的系统工具菜单栏、设置入口与内置工具分组，
/// 以及窗口几何、窗口外点击收起等基本交互。
/// 用法：<c>DevEnv.exe --shortcuts-check %TEMP%\shortcuts.txt</c>
/// </summary>
public static class ShortcutLauncherProbe
{
    public const string Argument = "--shortcuts-check";

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
            Check("菜单栏含设置入口",
                window.SettingsMenuItem.Header?.ToString()?.Contains("设置") == true,
                $"Header={window.SettingsMenuItem.Header}");
            Check("菜单栏含帮助入口",
                window.MainMenu.Items.OfType<MenuItem>()
                    .Any(item => item.Header?.ToString()?.Contains("帮助") == true));
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

            // 菜单必须真的能点开，并且分类 / 叶子项都生成了容器。
            // 探针窗口不抢焦点；如果当前前台另有窗口，WPF 不允许弹层，记为 SKIP 而不是失败。
            window.UpdateLayout();
            Pump();
            window.Activate();
            Pump();
            window.SystemToolsMenuItem.IsSubmenuOpen = true;
            Pump();
            var menuOpened = window.SystemToolsMenuItem.IsSubmenuOpen;
            var groupItem = window.SystemToolsMenuItem.ItemContainerGenerator
                .ContainerFromIndex(0) as MenuItem;
            if (menuOpened)
            {
                Check("系统工具菜单可展开并生成分类项", groupItem is not null);
                if (groupItem is not null)
                {
                    groupItem.IsSubmenuOpen = true;
                    Pump();
                    var leafItem = groupItem.ItemContainerGenerator.ContainerFromIndex(0) as MenuItem;
                    Check("系统工具叶子项已生成且带条目数据",
                        leafItem is { DataContext: LauncherEntry },
                        leafItem is null ? "叶子容器为 null" : $"Header={leafItem.Header?.GetType().Name ?? "null"}");
                }
            }
            else
            {
                report.AppendLine(
                    $"SKIP  系统工具菜单弹层（探针窗口未获得前台，items={window.SystemToolsMenuItem.Items.Count}）");
            }

            window.CloseMenusForProbe();
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
            Check("内置工具不再包含快捷方式窗口入口",
                builtinKeys.All(key => !key.StartsWith("shortcut-", StringComparison.OrdinalIgnoreCase)),
                string.Join(" / ", builtinKeys));
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
            WaitUntil(() => window.SystemToolsMenuItem.IsSubmenuOpen, 2_000);
            Pump();
            Check("DEVENV_START_PAGE=tools 切到内置工具页",
                vm.SelectedPage == 4 && !vm.IsInsideGroup && !vm.IsSearching,
                $"SelectedPage={vm.SelectedPage}");
            if (window.SystemToolsMenuItem.IsSubmenuOpen)
                report.AppendLine("OK    tools 路由展开系统工具菜单");
            else
                report.AppendLine("SKIP  tools 路由菜单展开（探针窗口未获得前台）");

            window.CloseMenusForProbe();
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
            Check("主窗口不是置顶窗口", !window.Topmost);

            // 菜单可能因前台限制残留打开状态，先清掉再验证窗口外点击
            window.CloseMenusForProbe();
            Pump();

            // 从主界面打开的子窗口还开着时，主界面必须保持可见
            var child = new Window
            {
                Owner = window,
                Width = 200,
                Height = 120,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000,
            };
            child.Show();
            Pump();
            window.SimulateOutsideClick();
            Check("子工具窗打开时主界面不因点外面收起", window.IsVisible);
            child.Close();
            Pump();
            WaitUntil(
                () => !Application.Current!.Windows.OfType<Window>().Any(
                    w => !ReferenceEquals(w, window) && w.IsVisible && ReferenceEquals(w.Owner, window)),
                2_000);
            window.CloseMenusForProbe();
            Pump();

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
