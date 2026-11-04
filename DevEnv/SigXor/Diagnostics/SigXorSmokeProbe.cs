using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DevEnv.Host.SigXor.Diagnostics;

/// <summary>
/// 窗口 / 视图冒烟探针：逐一实例化 SigXor 模块的所有窗口与视图，捕获加载期异常。
/// 用法：DevEnv.exe --sigxor-smoke [报告路径]
///
/// 用途：页面级 XAML 异常在 WPF 里是静默的（AGENTS.md 第 12 条），
/// 合并进来的 SigXor 有 34 个窗口 / 视图，必须能自动验证。
/// </summary>
public static class SigXorSmokeProbe
{
    public const string Argument = "--sigxor-smoke";

    public static int Run(string[] args)
    {
        var report = new System.Text.StringBuilder();
        int failures = 0;

        void Probe(string name, Action action)
        {
            try
            {
                action();
                report.AppendLine($"OK    {name}");
            }
            catch (Exception ex)
            {
                failures++;
                report.AppendLine($"FAIL  {name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        var screens = new Services.WpfScreenService();

        // ---- 预览视图（按 PreviewViewModel 的内容类型分发）----
        Probe("Views.PreviewPane", () => new Views.PreviewPane());
        Probe("Views.TextView", () => new Views.TextView());
        Probe("Views.HexEditorView", () => new Views.HexEditorView());
        Probe("Views.ImageView", () => new Views.ImageView());
        Probe("Views.InfoView", () => new Views.InfoView());
        Probe("Views.OfficeView", () => new Views.OfficeView());
        Probe("Views.MarkdownView", () => new Views.MarkdownView());
        Probe("Views.HtmlView", () => new Views.HtmlView());
        Probe("Views.AudioView", () => new Views.AudioView());
        Probe("Views.PdfView", () => new Views.PdfView());
        Probe("Views.ArchiveView", () => new Views.ArchiveView());
        Probe("Views.CsvView", () => new Views.CsvView());
        Probe("Views.FontView", () => new Views.FontView());
        Probe("Views.SvgView", () => new Views.SvgView());
        Probe("Views.VideoView", () => new Views.VideoView());

        // ---- WebView2 宿主 ----
        Probe("Views.HtmlWebHost", () => new Views.HtmlWebHost());
        Probe("Views.MarkdownWebHost", () => new Views.MarkdownWebHost());
        Probe("Views.OfficeWebHost", () => new Views.OfficeWebHost());
        Probe("Views.AudioWebHost", () => new Views.AudioWebHost());
        Probe("Views.PdfWebHost", () => new Views.PdfWebHost());
        Probe("Views.SvgWebHost", () => new Views.SvgWebHost());
        Probe("Views.VideoWebHost", () => new Views.VideoWebHost());

        // ---- 窗口 ----
        Probe("Views.ShellPreviewWindow", () => new Views.ShellPreviewWindow());
        Probe("Views.LauncherWindow", () =>
            new Views.LauncherWindow(new Services.LauncherService(), () => { }));
        Probe("MainWindow", () => new MainWindow());
        Probe("SettingsWindow", () => new SettingsWindow(new Services.SettingsService()));
        Probe("UpdateWindow", () => new UpdateWindow(new ViewModels.UpdateViewModel(new Services.UpdateService())));
        Probe("AboutWindow", () => new AboutWindow(() => { }));

        // ---- 工具窗口 ----
        Probe("Toolkit.Capture.RegionSelectorWindow", () =>
            new Toolkit.Capture.RegionSelectorWindow(null!, default, 1.0));
        Probe("Toolkit.Capture.ScreenshotPreviewWindow", () => new Toolkit.Capture.ScreenshotPreviewWindow());
        Probe("Toolkit.Capture.ScreenshotToolbarWindow", () => new Toolkit.Capture.ScreenshotToolbarWindow(screens));
        Probe("Toolkit.Ocr.OcrResultWindow", () => new Toolkit.Ocr.OcrResultWindow(screens));
        Probe("Toolkit.Voice.VoiceOverlayWindow", () => new Toolkit.Voice.VoiceOverlayWindow(screens));

        string text = report.ToString();
        ProbeOutput.Line(text);
        ProbeOutput.Line($"失败 {failures} 项 / 共 {failures + CountOk(text)} 项");

        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            try { File.WriteAllText(args[0], ProbeOutput.Flush()); } catch { }
        }

        return failures == 0 ? 0 : 1;
    }

    private static int CountOk(string text) =>
        text.Split('\n').Count(l => l.StartsWith("OK ", StringComparison.Ordinal));
}
