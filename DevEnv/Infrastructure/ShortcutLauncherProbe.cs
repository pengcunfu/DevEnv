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
/// 快捷方式启动器自检：WPF 的 XAML / 绑定错误是静默的，肉眼看不出，
/// 这里加载启动器、遍历三个页签、跑一次搜索，并收集数据绑定错误。
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
            // 放到屏幕外、不抢激活，避免探针时真的铺满桌面
            window = new ShortcutsWindow
            {
                Left = -4000,
                Top = -4000,
                Width = 1100,
                Height = 720,
                Opacity = 0,
                ShowInTaskbar = false,
                ShowActivated = false,
            };
            window.Show();
            Pump();
            window.UpdateLayout();

            Check("启动器窗口可正常加载", window.IsVisible);
            if (window.DataContext is not ShortcutLauncherViewModel vm)
                throw new InvalidOperationException("DataContext 不是 ShortcutLauncherViewModel");

            Check("页签为横向三个", vm.Pages.Count == 3, string.Join(" / ", vm.Pages.Select(p => p.Title)));
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

            vm.Query = string.Empty;
            Pump();
            Check("清空搜索后退出搜索模式", !vm.IsSearching);

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
