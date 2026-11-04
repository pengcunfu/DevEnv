using System;
using System.IO;
using System.Threading.Tasks;

namespace DevEnv.Host.SigXor.Diagnostics;

/// <summary>
/// SigXor 模块自检入口：一次跑完三组探针。
///
/// 用法：
///   DevEnv.exe --sigxor-smoke [报告路径]          窗口 / 视图能否加载
///   DevEnv.exe --sigxor-preview-smoke [报告路径]  预览内容能否渲染
///   DevEnv.exe --sigxor-theme-smoke [报告路径]    深浅主题切换是否生效
///   DevEnv.exe --sigxor-check [报告路径]          以上全部
///
/// 为什么要探针：WPF 的页面级 XAML 异常是静默的（AGENTS.md 第 12 条），
/// 窗口不显示但没有报错。合并进来的 SigXor 有 34 个窗口 / 视图，
/// 靠肉眼点菜单验证不现实，必须有可重复的自动检查。
/// </summary>
public static class SigXorDiagnostics
{
    public const string Argument = "--sigxor-check";

    public static async Task<int> RunAllAsync(string[] args)
    {
        ProbeOutput.Section("=== 1/3 窗口与视图加载 ===");
        int failures = SigXorSmokeProbe.Run([]);

        ProbeOutput.Section("=== 2/3 预览内容渲染 ===");
        failures += await SigXorPreviewProbe.RunAsync([]);

        ProbeOutput.Section("=== 3/3 主题切换 ===");
        failures += SigXorThemeProbe.Run([]);

        ProbeOutput.Blank();
        ProbeOutput.Line(failures == 0 ? "全部通过" : $"共 {failures} 项失败");

        string text = ProbeOutput.Flush();
        try
        {
            File.WriteAllText(
                args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : "sigxor-check.txt",
                text);
        }
        catch
        {
            // 报告写不出去不影响退出码
        }

        return failures == 0 ? 0 : 1;
    }
}
