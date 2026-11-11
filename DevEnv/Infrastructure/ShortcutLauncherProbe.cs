using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using DevEnv.Host.SigXor.Diagnostics;
using DevEnv.Host.ViewModels;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 启动器自检：WPF 的 XAML / 绑定错误是静默的，肉眼看不出，
/// 这里加载启动器、遍历四个页签、展开两个工具文件夹、跑一次搜索，并收集数据绑定错误。
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
            // 预热必须落在最终几何上，否则 Show 时还要挪窗口，DWM 重新合成会闪
            var expected = ShortcutsWindow.ComputeScreenBounds();
            Check("预热使用最终几何（固定 960 × 680 DIP、居中，显示时不再移动窗口）",
                Math.Abs(window.Left - expected.Left) < 0.5
                && Math.Abs(window.Top - expected.Top) < 0.5
                && Math.Abs(window.Width - expected.Width) < 0.5
                && Math.Abs(window.Height - expected.Height) < 0.5,
                $"Left={window.Left}, Top={window.Top}, W={window.Width}, H={window.Height}, " +
                $"expected=({expected.Left},{expected.Top},{expected.Width},{expected.Height})");
            var area = CursorScreen.GetWorkAreaDip();
            Check("启动器为固定尺寸（960 × 680 DIP）",
                Math.Abs(window.Width - DevEnv.Core.Services.LauncherGeometry.Width) < 0.5
                && Math.Abs(window.Height - DevEnv.Core.Services.LauncherGeometry.Height) < 0.5,
                $"W={window.Width}, H={window.Height}, area={area.Width}×{area.Height}");
            // 居中：左右与上下留白相等（取整误差在 1 DIP 内）；工作区跟鼠标所在屏
            var leftGap = window.Left - area.Left;
            var rightGap = area.Left + area.Width - (window.Left + window.Width);
            var topGap = window.Top - area.Top;
            var bottomGap = area.Top + area.Height - (window.Top + window.Height);
            Check("启动器在鼠标所在屏工作区内水平、垂直居中",
                Math.Abs(leftGap - rightGap) <= 1 && Math.Abs(topGap - bottomGap) <= 1,
                $"left={leftGap:F1}, right={rightGap:F1}, top={topGap:F1}, bottom={bottomGap:F1}");
            // 配色跟随系统应用模式：浅 / 深两套 key 一致，读不到时应至少能解析到画刷
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
            Check("窗口标题为「FireNeb DevEnv」", window.Title == "FireNeb DevEnv", $"Title={window.Title}");
            // 启动器不挂 owner：挂上主窗后从托盘唤起会把主窗一起拉出来
            Check("启动器不挂 Owner（不会带出主窗口）", window.Owner is null);
            // 窗口改用 Windows 默认外观：系统标题栏与边框，不再自绘透明分层窗口
            Check("使用 Windows 默认窗口（标准边框与标题栏）",
                window.WindowStyle == WindowStyle.SingleBorderWindow && !window.AllowsTransparency,
                $"WindowStyle={window.WindowStyle}, AllowsTransparency={window.AllowsTransparency}");
            if (window.DataContext is not ShortcutLauncherViewModel vm)
                throw new InvalidOperationException("DataContext 不是 ShortcutLauncherViewModel");

            // 快捷方式与开始菜单都在后台加载；等索引落定再断言（图标可继续分批补齐）
            WaitUntil(() => !vm.IsShortcutDataLoading, 15_000);
            WaitUntil(() => !vm.IsStartMenuLoading, 15_000);

            Check("页签为纵向六个", vm.Pages.Count == 6, string.Join(" / ", vm.Pages.Select(p => p.Title)));
            Check("默认显示本地应用", vm.SelectedPage == 0);
            Check("页签与当前页同步", window.PageTabs.SelectedIndex == vm.SelectedPage);
            var tabsLeft = window.PageTabs.TranslatePoint(new Point(0, 0), window.Root).X;
            var tabsTop = window.PageTabs.TranslatePoint(new Point(0, 0), window.Root).Y;
            var searchLeft = window.SearchShell.TranslatePoint(new Point(0, 0), window.Root).X;
            var searchTop = window.SearchShell.TranslatePoint(new Point(0, 0), window.Root).Y;
            var searchBottom = searchTop + window.SearchShell.ActualHeight;
            var searchRight = searchLeft + window.SearchShell.ActualWidth;
            Check("搜索框与页签排在左侧栏",
                searchLeft < window.Root.ActualWidth / 4
                && tabsLeft < window.Root.ActualWidth / 4
                && searchBottom <= tabsTop + 0.5
                && Math.Abs(searchLeft - tabsLeft) <= 12,
                $"search=({searchLeft:F1},{searchTop:F1}) tabs=({tabsLeft:F1},{tabsTop:F1})");
            Check("搜索框落在左侧栏宽度内",
                searchRight <= window.Root.ActualWidth / 3,
                $"searchRight={searchRight:F1}, rootWidth={window.Root.ActualWidth:F1}");
            Check("图标网格已生成卡片容器",
                vm.VisibleEntries.Count == 0 || window.EntryGrid.ItemContainerGenerator.ContainerFromIndex(0) is not null);
            // 右侧内容区 ≈ 客户区 − 196（侧栏）− 8（滚动区内边距）；卡片宽 90 → 每行 8 个
            var gridWidth = window.EntryScroller.ActualWidth;
            var columns = gridWidth > 0 ? (int)(gridWidth / 90d) : 0;
            Check("右侧网格每行可排 8 个图标",
                columns >= 8,
                $"gridWidth={gridWidth:F1}, columns={columns}");

            foreach (var page in vm.Pages)
            {
                vm.SelectedPage = page.Index;
                window.UpdateLayout();
                Pump();
                Check($"页签「{page.Title}」可切换（{vm.VisibleEntries.Count} 项）",
                    vm.SelectedPage == page.Index && window.PageTabs.SelectedIndex == page.Index);
            }

            // 开始菜单页签是 Windows 开始菜单的只读投影：只能启动或收进启动器
            vm.SelectedPage = 5;
            window.UpdateLayout();
            WaitUntil(() => !vm.IsStartMenuLoading, 10_000);
            Pump();
            Check("开始菜单页只读（无添加 / 框选 / 分组）",
                vm.IsStartMenuPage
                && !vm.IsEditableView
                && !vm.CanCreateGroup
                && !vm.CanAddOnCurrentPage
                && !vm.CanAcceptExternalDrop);
            Check("开始菜单页顶层条目类型正确",
                vm.VisibleEntries.All(entry =>
                    entry.Kind is ShortcutKind.StartMenu or ShortcutKind.Group),
                $"共 {vm.VisibleEntries.Count} 项");
            window.ShowPage("startmenu");
            WaitUntil(() => !vm.IsStartMenuLoading, 10_000);
            Pump();
            Check("DEVENV_START_PAGE=startmenu 切到开始菜单页",
                vm.SelectedPage == 5 && !vm.IsInsideGroup && !vm.IsSearching);
            window.ShowPage("apps");
            Pump();

            vm.SelectedPage = 3;
            Pump();
            Check("系统工具页签载入完整的 Windows 工具目录",
                vm.IsSystemToolsPage
                && vm.VisibleEntries.Count > 0
                && vm.VisibleEntries.All(e => e.Kind == ShortcutKind.Tool),
                $"共 {vm.VisibleEntries.Count} 项");
            Check("系统工具页签只读", !vm.IsEditableView && !vm.CanAddOnCurrentPage && !vm.CanCreateGroup);

            vm.SelectedPage = 4;
            Pump();
            Check("内置工具页签载入宿主工具目录",
                vm.IsBuiltinToolsPage
                && vm.VisibleEntries.Count == 18
                && vm.VisibleEntries.All(e => e.Kind == ShortcutKind.InternalTool),
                $"共 {vm.VisibleEntries.Count} 项");
            Check("内置工具都带彩色图标",
                vm.VisibleEntries.All(e => e.HasToolBadge && !e.ShowsKindGlyph),
                $"缺图标 {vm.VisibleEntries.Count(e => !e.HasToolBadge)} 项");
            var builtinKeys = vm.VisibleEntries.Select(e => e.Key).ToList();
            Check("内置工具页签含环境与监控独立入口",
                builtinKeys.Contains("devenv")
                && builtinKeys.Contains("lava")
                && builtinKeys.Contains("network")
                && builtinKeys.Contains("processes")
                && builtinKeys.Contains("services")
                && builtinKeys.Contains("ports")
                && builtinKeys.Contains("speed"),
                string.Join(" / ", builtinKeys));
            Check("内置工具页签含翻译 / 剪贴板 / 文件浏览器入口",
                builtinKeys.Contains("translator")
                && builtinKeys.Contains("clipboard")
                && builtinKeys.Contains("browser"),
                string.Join(" / ", builtinKeys));
            Check("内置工具页签只读", !vm.IsEditableView);

            vm.SelectedPage = 0;
            vm.Query = string.Empty;
            Pump();
            Check("本地应用页允许框选与拖动", vm.IsEditableView && vm.CanCreateGroup);
            Check("每个页签的卡片标识不重复",
                vm.Pages.All(p =>
                {
                    vm.SelectedPage = p.Index;
                    Pump();
                    var keys = vm.VisibleEntries.Select(e => e.Key).ToList();
                    return keys.Count == keys.Distinct(StringComparer.OrdinalIgnoreCase).Count();
                }));

            vm.SelectedPage = 0;
            Pump();
            if (vm.VisibleEntries.FirstOrDefault() is { } selectable)
            {
                vm.SetSelection([selectable]);
                Check("选中卡片后记录选择", vm.HasSelection && vm.SelectedEntries.Count == 1);
                vm.ClearSelection();
                Check("清空选择", !vm.HasSelection);
            }

            vm.SelectedPage = 0;
            Pump();
            if (vm.AddDiagnosticGroup())
            {
                window.UpdateLayout();
                Pump();
                var group = vm.VisibleEntries.FirstOrDefault(entry => entry.IsGroup);
                Check("启动器文件夹磁贴可渲染（含 2×2 预览）", group is not null);
                if (group is not null)
                {
                    vm.OpenGroup(group);
                    Pump();
                    Check("打开启动器文件夹后显示组内条目",
                        vm.IsInsideGroup && vm.VisibleEntries.Count == group.GroupItems.Count);
                    Check("文件夹浮层打开时底层页签卡片保留",
                        vm.PageEntries.Count > 0 && vm.PageEntries.Any(entry => entry.IsGroup));
                    Check("文件夹内禁止再建文件夹", !vm.CanCreateGroup);
                    vm.CloseGroup();
                    Pump();
                    Check("返回后回到页签视图", !vm.IsInsideGroup);
                }

                vm.RemoveDiagnosticGroup();
                Pump();
            }
            else
            {
                report.AppendLine("SKIP  本地应用不足 2 项，跳过启动器文件夹检查");
            }

            vm.SelectedPage = 0;
            vm.Query = string.Empty;
            Pump();
            if (vm.VisibleEntries.FirstOrDefault() is { } sample)
            {
                var needle = sample.Name.Length > 2 ? sample.Name[..2] : sample.Name;
                vm.Query = needle;
                Pump();
                Check($"搜索「{needle}」能命中", vm.IsSearching && vm.VisibleEntries.Any(e => e.Name == sample.Name));
            }

            vm.Query = "JSON";
            Pump();
            Check("搜索能命中内置工具", vm.IsSearching && vm.VisibleEntries.Any(e => e.Key == "json"));
            vm.Query = string.Empty;
            Pump();

            vm.Query = "__launcher_probe_no_match__";
            Pump();
            Check("搜索无匹配时显示空状态", vm.IsSearching && vm.IsEmpty);
            Check("搜索态禁止框选与拖动", !vm.IsEditableView);

            vm.Query = string.Empty;
            Pump();
            Check("清空搜索后退出搜索模式", !vm.IsSearching);

            // 环境变量路由：tools / builtin 对应页签索引
            window.ShowPage("tools");
            WaitUntil(() => !vm.IsShortcutDataLoading, 15_000);
            Pump();
            Check("DEVENV_START_PAGE=tools 切到系统工具页签",
                vm.SelectedPage == 3 && vm.IsSystemToolsPage && !vm.IsInsideGroup && !vm.IsSearching);
            Check("系统工具按分类标签展示",
                vm.ShowsToolSections
                && vm.ToolSections.Count >= 2
                && vm.ToolSections.All(section => !string.IsNullOrWhiteSpace(section.Title) && section.Entries.Count > 0),
                $"sections={vm.ToolSections.Count}: {string.Join(", ", vm.ToolSections.Select(s => $"{s.Title}×{s.Entries.Count}"))}");
            window.ShowPage("builtin");
            WaitUntil(() => !vm.IsShortcutDataLoading, 15_000);
            Pump();
            Check("DEVENV_START_PAGE=builtin 切到内置工具页签",
                vm.SelectedPage == 4 && vm.IsBuiltinToolsPage && !vm.IsInsideGroup && !vm.IsSearching);
            Check("内置工具按分类标签展示",
                vm.ShowsToolSections
                && vm.ToolSections.Count >= 4
                && vm.ToolSections.Any(section => section.Title == "环境")
                && vm.ToolSections.Any(section => section.Title == "监控")
                && vm.ToolSections.Any(section => section.Title == "窗口入口")
                && vm.ToolSections.Any(section => section.Title == "实用工具"),
                $"sections={string.Join(", ", vm.ToolSections.Select(s => s.Title))}");
            window.ShowPage("apps");
            WaitUntil(() => !vm.IsShortcutDataLoading, 15_000);
            Pump();
            Check("回到 apps 页签", !vm.IsInsideGroup && vm.SelectedPage == 0);
            Check("ShowPage 可见时安装窗口外点击监听", window.IsOutsideClickHookInstalled);

            // 窗口外点击必须真的收起，而不是只装了钩子；标准窗口直接隐藏，没有退出动画
            window.SimulateOutsideClick();
            Check("窗口外点击立即收起并隐藏", !window.IsVisible);
            Check("窗口外点击后卸载监听", !window.IsOutsideClickHookInstalled);
            window.ShowPage("apps");
            Pump();
            Check("窗口外点击收起后可以重新打开",
                window.IsVisible && window.IsOutsideClickHookInstalled,
                $"visible={window.IsVisible}, hook={window.IsOutsideClickHookInstalled}");

            // 收起 → 再打开（第二次打开正是用户反馈会闪的地方）：标准窗口不播退出动画，直接 Hide
            window.ShowPage("builtin");
            WaitUntil(() => !vm.IsShortcutDataLoading, 15_000);
            Pump();
            window.Dismiss();
            Check("收起立即隐藏（无退出动画）", !window.IsVisible);
            Check("收起后卸载窗口外点击监听", !window.IsOutsideClickHookInstalled);

            // 托盘 / 左 Alt / 显示主窗口不传 pageId，应留在隐藏前的页签
            window.ShowPage(null);
            WaitUntil(() => !vm.IsShortcutDataLoading, 15_000);
            Pump();
            Check("再次打开仍正常显示", window.IsVisible, $"visible={window.IsVisible}");
            Check("再次打开保留上次页签",
                vm.SelectedPage == 4 && vm.IsBuiltinToolsPage,
                $"SelectedPage={vm.SelectedPage}");

            window.ShowPage("apps");
            Pump();

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

    /// <summary>反复跑消息循环直到条件成立（最多等 timeoutMs）。后台加载与异步绑定需要泵消息。</summary>
    private static void WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
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
