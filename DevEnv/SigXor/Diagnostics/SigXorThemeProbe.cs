using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using DevEnv.Host.Themes;

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
}
