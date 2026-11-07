using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using DevEnv.Core.Services;
using DevEnv.Host.SigXor.Diagnostics;
using DevEnv.Host.ViewModels;
using DevEnv.Host.Views;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 主窗口拆分自检：开发窗口（开发环境 / 熔岩环境）与监控窗口（网络 / 进程 / 服务监控）
/// 共用同一套界面，但各自只带自己的导航项，切页互不影响。
/// 用法：<c>DevEnv.exe --windows-check %TEMP%\windows.txt</c>
/// </summary>
public static class MainWindowProbe
{
    public const string Argument = "--windows-check";

    private const string DevelopmentTitle = "燧岚开发盒子";
    private const string MonitorTitle = "燧岚监控中心";

    public static int Run(string[] args)
    {
        var report = new StringBuilder();
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

        // 主窗视图会读 LavaEnv 目录与配置，探针进程里也要先把服务初始化起来
        CoreServices.Initialize();

        try
        {
            var dev = Create(DevelopmentTitle, ViewFactory.DevelopmentNavItems);
            var monitor = Create(MonitorTitle, ViewFactory.MonitorNavItems);

            Check("开发窗口可加载", dev.IsVisible && dev.ViewModel.CurrentView is not null);
            Check("监控窗口可加载", monitor.IsVisible && monitor.ViewModel.CurrentView is not null);
            Check("两个窗口标题不同", dev.Title == DevelopmentTitle && monitor.Title == MonitorTitle,
                $"{dev.Title} / {monitor.Title}");

            Check("开发窗口导航为开发环境与熔岩环境",
                dev.ViewModel.NavItems.Select(n => n.Id).SequenceEqual(["devenv", "lava"]),
                string.Join(" / ", dev.ViewModel.NavItems.Select(n => n.Id)));
            Check("监控窗口导航为网络、进程与服务监控",
                monitor.ViewModel.NavItems.Select(n => n.Id).SequenceEqual(["network", "processes", "services"]),
                string.Join(" / ", monitor.ViewModel.NavItems.Select(n => n.Id)));

            // 字形必须是单个 MDL2 私用区字符；若误写成双反斜杠转义，Length 会是 7
            Check("导航图标是西文字形而不是字面量转义",
                dev.ViewModel.NavItems.Concat(monitor.ViewModel.NavItems)
                    .All(nav => nav.Glyph.Length == 1),
                string.Join(" / ", dev.ViewModel.NavItems.Concat(monitor.ViewModel.NavItems)
                    .Select(nav => $"{nav.Glyph}({nav.Glyph.Length})")));

            Check("开发窗口默认停在开发环境", dev.ViewModel.CurrentPageId == "devenv", dev.ViewModel.CurrentPageId);
            Check("监控窗口默认停在网络监控", monitor.ViewModel.CurrentPageId == "network", monitor.ViewModel.CurrentPageId);

            monitor.NavigateTo("services");
            Pump();
            Check("监控窗口可切到服务监控", monitor.ViewModel.CurrentPageId == "services", monitor.ViewModel.CurrentPageId);
            Check("监控窗口切页不影响开发窗口", dev.ViewModel.CurrentPageId == "devenv", dev.ViewModel.CurrentPageId);

            dev.NavigateTo("lava");
            Pump();
            Check("开发窗口可切到熔岩环境", dev.ViewModel.CurrentPageId == "lava", dev.ViewModel.CurrentPageId);
            Check("开发窗口切页不影响监控窗口", monitor.ViewModel.CurrentPageId == "services", monitor.ViewModel.CurrentPageId);
        }
        catch (Exception ex)
        {
            failures++;
            report.AppendLine($"FAIL  探针异常: {ex.GetType().Name}: {ex.Message}");
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

    private static MainWindow Create(string title, IReadOnlyList<NavItem> navItems)
    {
        var window = new MainWindow(title, navItems)
        {
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -4000,
            Top = -4000,
        };
        window.Show();
        window.UpdateLayout();
        Pump();
        return window;
    }

    /// <summary>跑一轮消息循环，让布局与绑定更新完成。</summary>
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
