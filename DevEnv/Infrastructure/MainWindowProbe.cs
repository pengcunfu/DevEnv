using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using DevEnv.Core.Services;
using DevEnv.Host.SigXor.Diagnostics;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 功能工具窗自检：开发环境 / 熔岩环境 / 网络 / 进程 / 服务 / 端口 / 测速
/// 各自作为独立 ToolWindow 打开，互不影响。
/// 用法：<c>DevEnv.exe --windows-check %TEMP%\windows.txt</c>
/// </summary>
public static class MainWindowProbe
{
    public const string Argument = "--windows-check";

    private static readonly (string Id, string Title)[] ExpectedTools =
    [
        ("devenv", "开发环境"),
        ("lava", "熔岩环境"),
        ("network", "网络监控"),
        ("processes", "进程监控"),
        ("services", "服务监控"),
        ("ports", "端口管理"),
        ("speed", "网络测速"),
    ];

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

        // 工具视图会读 LavaEnv 目录与配置，探针进程里也要先把服务初始化起来
        CoreServices.Initialize();

        try
        {
            foreach (var (id, title) in ExpectedTools)
            {
                Check($"工具 id「{id}」已注册", ToolWindowHost.IsTool(id));
                var result = ToolWindowHost.Open(id);
                Check($"打开「{title}」", result.Ok, result.DisplayMessage);
                Pump();

                var window = ToolWindowHost.Find(id);
                Check($"「{title}」窗口可加载",
                    window is { IsVisible: true } && window.Title == title,
                    window is null ? "窗口为 null" : $"Title={window.Title} Visible={window.IsVisible}");
            }

            // 再打开一次应前置已有实例，而不是新建
            var before = ToolWindowHost.Find("network");
            ToolWindowHost.Open("network");
            Pump();
            Check("重复打开网络监控仍是同一实例",
                ReferenceEquals(before, ToolWindowHost.Find("network")));

            ToolWindowHost.CloseAll();
            Pump();
            Check("关闭全部工具窗后找不到网络监控", ToolWindowHost.Find("network") is null);
        }
        catch (Exception ex)
        {
            failures++;
            report.AppendLine($"FAIL  探针异常: {ex.GetType().Name}: {ex.Message}");
            try { ToolWindowHost.CloseAll(); } catch { /* 探针收尾 */ }
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
