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
/// 全局启动器自检：WPF 的 XAML / 绑定错误是静默的，肉眼看不出，
/// 这里加载启动器、遍历四个页签、跑一次搜索，并收集数据绑定错误。
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
            Check("预热使用最终几何（显示时不再移动窗口）",
                Math.Abs(window.Left) < 0.5 && Math.Abs(window.Top) < 0.5,
                $"Left={window.Left}, Top={window.Top}");
            // 隐藏时内容必须留在全透明：分层窗口缓存的最后一帧会直接在下次 Show 时露出
            Check("预热后内容保持全透明（缓存的表层不会闪）",
                window.Root.Opacity < 0.001, $"Root.Opacity={window.Root.Opacity}");

            // 后面的渲染检查放到屏幕外，避免真的盖住用户桌面
            window.Left = -4000;
            window.Top = -4000;
            window.Show();
            window.Root.Opacity = 1;
            Pump();
            window.UpdateLayout();

            Check("启动器窗口可正常加载", window.IsVisible);
            // 全局启动器不挂 owner：挂上主窗后从托盘唤起会把主窗一起拉出来
            Check("全局启动器不挂 Owner（不会带出主窗口）", window.Owner is null);
            // 淡入必须做在根元素上：分层窗口改自己的 Opacity 会先画一帧不透明再淡入，闪两下
            Check("淡入作用在根元素上（窗口 Opacity 恒为 1）", Math.Abs(window.Opacity - 1) < 0.001,
                $"Opacity={window.Opacity}");
            if (window.DataContext is not ShortcutLauncherViewModel vm)
                throw new InvalidOperationException("DataContext 不是 ShortcutLauncherViewModel");

            Check("页签为横向四个", vm.Pages.Count == 4, string.Join(" / ", vm.Pages.Select(p => p.Title)));
            Check("默认显示本地应用", vm.SelectedPage == 0);
            Check("底部页签与当前页同步", window.PageTabs.SelectedIndex == vm.SelectedPage);
            Check("图标网格已生成卡片容器",
                vm.VisibleEntries.Count == 0 || window.EntryGrid.ItemContainerGenerator.ContainerFromIndex(0) is not null);

            foreach (var page in vm.Pages)
            {
                vm.SelectedPage = page.Index;
                window.UpdateLayout();
                Pump();
                Check($"页签「{page.Title}」可切换（{vm.VisibleEntries.Count} 项）",
                    vm.SelectedPage == page.Index && window.PageTabs.SelectedIndex == page.Index);
            }

            // 系统工具页签应载入完整的 Windows 工具目录，并且全部是 Tool 类型
            var toolsPage = vm.Pages.First(p => p.Title == "系统工具");
            vm.SelectedPage = toolsPage.Index;
            Pump();
            Check("系统工具页签加载工具目录",
                vm.VisibleEntries.Count > 0 && vm.VisibleEntries.All(e => e.Kind == ShortcutKind.Tool),
                $"共 {vm.VisibleEntries.Count} 项");
            Check("系统工具页签隐藏「添加」按钮", !vm.CanAddOnCurrentPage);
            Check("系统工具页签禁止框选与拖动", !vm.IsEditableView);

            vm.SelectedPage = 0;
            vm.Query = string.Empty;
            Pump();
            Check("本地应用页允许框选与拖动", vm.IsEditableView && vm.CanCreateGroup);
            Check("每个页签的卡片标识不重复",
                vm.Pages.Where(p => p.Index != toolsPage.Index).All(p =>
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

            vm.Query = "__launcher_probe_no_match__";
            Pump();
            Check("搜索无匹配时显示空状态", vm.IsSearching && vm.IsEmpty);
            Check("搜索态禁止框选与拖动", !vm.IsEditableView);

            vm.Query = string.Empty;
            Pump();
            Check("清空搜索后退出搜索模式", !vm.IsSearching);

            // 收起 → 再打开（第二次打开正是用户反馈会闪的地方）：
            // 收起必须先淡出到全透明再隐藏，否则分层窗口缓存的是不透明画面，下次 Show 会闪。
            window.Dismiss();
            // 关键顺序：先淡出、窗口仍然可见，淡完才 Hide。
            // 若先 Hide 再把内容设成全透明，窗口已不可见、不会重新渲染，
            // 分层窗口缓存里仍是不透明画面，下次 Show 会闪出旧画面。
            Check("收起时先淡出、窗口仍可见（缓存才会更新为透明）",
                window.IsHiding && window.IsVisible);
            WaitUntil(() => !window.IsVisible);
            Check("收起完成并隐藏", !window.IsVisible);
            Check("收起后缓存为全透明（下次 Show 不会闪出旧画面）",
                window.Root.Opacity < 0.001, $"Root.Opacity={window.Root.Opacity}");

            window.ShowPage("apps");
            Pump();
            Check("再次打开仍正常显示", window.IsVisible && !window.IsHiding);

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

    /// <summary>反复跑消息循环直到条件成立（最多等 timeoutMs）。动画收尾依赖计时器，必须泵消息。</summary>
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
