using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using DevEnv.Host.Themes;
using ICSharpCode.AvalonEdit.Highlighting;

namespace DevEnv.Host.SigXor.Diagnostics;

/// <summary>
/// 主题探针：验证深色 / 浅色切换真的替换了调色板字典，且 SigXor 样式字典没被挤掉。
/// 用法：DevEnv.exe --sigxor-theme-smoke [报告路径]
///
/// 背景：ThemeManager.Apply 只替换 MergedDictionaries 的最后一个字典位置。
/// 往 App.xaml 里加字典时若把调色板挤到中间，深色主题切换会静默失效
/// （浅色调色板残留），页面级问题在 WPF 里不会报错，只能这样验。
/// </summary>
public static class SigXorThemeProbe
{
    public const string Argument = "--sigxor-theme-smoke";

    public static int Run(string[] args)
    {
        var report = new System.Text.StringBuilder();
        int failures = 0;

        void Check(string name, bool ok, string detail)
        {
            if (!ok) failures++;
            report.AppendLine($"{(ok ? "OK    " : "FAIL  ")}{name}: {detail}");
        }

        var app = Application.Current;
        if (app is null)
        {
            report.AppendLine("FAIL  没有 Application 实例");
            ProbeOutput.Line(report.ToString());
            return 1;
        }

        // 让自定义语法高亮先按当前主题注册一次，后面切换主题时才能验证重新注册
        Services.SyntaxHighlightingService.Initialize();

        var dicts = app.Resources.MergedDictionaries;
        report.AppendLine($"字典数量: {dicts.Count}");
        foreach (var d in dicts)
            report.AppendLine($"  - {d.Source?.OriginalString ?? "(inline)"}");

        // 调色板必须在最后一位，否则 ThemeManager 替换不到它
        string? last = dicts.Count > 0 ? dicts[^1].Source?.OriginalString : null;
        Check("调色板位于字典末尾",
            last is not null && last.EndsWith("Palette.Light.xaml", StringComparison.OrdinalIgnoreCase),
            $"最后一个字典 = {last ?? "(null)"}");

        // SigXor 样式字典必须在
        bool hasSigXorStyles = dicts.Any(d =>
            (d.Source?.OriginalString ?? "").EndsWith("SigXorStyles.xaml", StringComparison.OrdinalIgnoreCase));
        Check("SigXor 样式字典存在", hasSigXorStyles, $"存在 = {hasSigXorStyles}");

        // 切到深色，看 AppBackgroundBrush 是否真的变了
        Brush before = TryGetBrush(app, "AppBackgroundBrush");
        ThemeManager.Apply(true);
        Brush dark = TryGetBrush(app, "AppBackgroundBrush");
        ThemeManager.Apply(false);
        Brush light = TryGetBrush(app, "AppBackgroundBrush");

        string Hex(Brush b) => b is SolidColorBrush s ? s.Color.ToString() : b.GetType().Name;
        Check("深色主题切换生效", Hex(dark) != Hex(light), $"深={Hex(dark)} 浅={Hex(light)}");

        // 深色下 AppBackground 应为深色
        Check("深色主题背景是深色",
            dark is SolidColorBrush ds && ds.Color.R < 0x40,
            $"深色背景 = {Hex(dark)}");

        // 切回浅色后应恢复
        Check("切回浅色恢复", Hex(light) == Hex(before), $"恢复后={Hex(light)} 原始={Hex(before)}");

        // SigXor 的 EngineToggle 样式键必须能解析
        object? toggle = app.TryFindResource("EngineToggle");
        Check("EngineToggle 样式可解析", toggle is not null, $"类型 = {toggle?.GetType().Name ?? "(null)"}");

        // 文本预览的编辑器底色必须跟随主题，浅色主题下不能还是深底
        var textView = new Views.TextView
        {
            DataContext = new ViewModels.TextContentViewModel(
                "probe.txt", "hello", DevEnv.Core.SigXor.EncodingService.Utf8,
                new Services.BackupService(new Services.SettingsService())),
        };
        var textHost = new Window
        {
            Content = textView,
            Width = 400,
            Height = 300,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Opacity = 0.01,
        };
        textHost.Show();
        textHost.UpdateLayout();

        ThemeManager.Apply(false);
        Brush lightEditor = textView.Editor.Background;
        ThemeManager.Apply(true);
        Brush darkEditor = textView.Editor.Background;
        textHost.Close();

        Check("浅色主题下文本编辑器为浅底",
            lightEditor is SolidColorBrush lightBrush && lightBrush.Color.R > 0xB0,
            $"浅色编辑器 = {Hex(lightEditor)}");
        Check("深色主题下文本编辑器为深底",
            darkEditor is SolidColorBrush darkBrush && darkBrush.Color.R < 0x40,
            $"深色编辑器 = {Hex(darkEditor)}");
        Check("编辑器底色随主题切换",
            Hex(lightEditor) != Hex(darkEditor),
            $"浅={Hex(lightEditor)} 深={Hex(darkEditor)}");

        // 自定义语法高亮（JSON / TOML / YAML / Log）配色也要随主题重新注册
        ThemeManager.Apply(false);
        string lightYaml = FirstSpanColor(".yaml");
        ThemeManager.Apply(true);
        string darkYaml = FirstSpanColor(".yaml");
        Check("语法高亮配色随主题切换",
            lightYaml.Length > 0 && lightYaml != darkYaml,
            $"浅={lightYaml} 深={darkYaml}");

        // 探针结束后恢复浅色主题
        ThemeManager.Apply(false);

        string text = report.ToString();
        ProbeOutput.Line(text);
        ProbeOutput.Line($"失败 {failures} 项");

        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            try { File.WriteAllText(args[0], ProbeOutput.Flush()); } catch { }
        }

        return failures == 0 ? 0 : 1;
    }

    private static Brush TryGetBrush(Application app, string key) =>
        app.TryFindResource(key) as Brush
        ?? throw new InvalidOperationException($"资源 {key} 不存在");

    /// <summary>取自定义高亮定义里第一个 Span 的前景色，用于验证配色随主题变化。</summary>
    private static string FirstSpanColor(string extension)
    {
        var definition = HighlightingManager.Instance.GetDefinitionByExtension(extension);
        var span = definition?.MainRuleSet.Spans.FirstOrDefault();
        if (span?.SpanColor?.Foreground is SimpleHighlightingBrush simple)
            return simple.GetBrush(null!).ToString();
        return "";
    }
}
