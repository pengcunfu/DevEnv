using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HostViews = DevEnv.Host.Views;

namespace DevEnv.Host.SigXor.Diagnostics;

/// <summary>
/// 设置对话框探针：打开合并后的设置窗口，验证平铺的五页签都能加载并正确切换。
///
/// 用法：DevEnv.exe --settings-check [报告路径]
///
/// 为什么单独探针：设置窗口的页签内容是运行时注入的（SigXor 模块提供），
/// XAML 本身不报错，只有真正显示出来、切换页签才知道内容是否正常。
/// </summary>
public static class SettingsDialogProbe
{
    public const string Argument = "--settings-check";

    public static int Run(string[] args)
    {
        var report = new System.Text.StringBuilder();
        int failures = 0;

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

        Windows.SettingsWindow? window = null;
        try
        {
            var settings = new Services.SettingsService();
            var generalView = new Views.SigXorGeneralView(settings);
            var hotkeysView = new HostViews.HotkeysSettingsView(settings);
            var voiceView = new Views.SigXorVoiceView(settings);
            var serviceView = new Views.SigXorServiceView();

            window = new Windows.SettingsWindow();
            window.SetGeneralContent(generalView);
            window.SetHotkeysContent(hotkeysView);
            window.SetVoiceContent(voiceView);
            window.SetServiceContent(serviceView);

            // 页签数量与标题：平铺五页签，不做二级
            var tabs = FindTabControl(window);
            Check("设置窗口包含 TabControl", tabs is not null);
            if (tabs is null)
                throw new InvalidOperationException("未找到 TabControl");

            var headers = tabs.Items.OfType<TabItem>().Select(t => (t.Header as string) ?? "").ToList();
            Check("页签为平铺五层", headers.Count == 5, string.Join(" / ", headers));
            Check("页签含「常规」", headers.Contains("常规"), string.Join(" / ", headers));
            Check("页签含「快捷键」", headers.Contains("快捷键"), string.Join(" / ", headers));
            Check("页签含「语音与截屏」", headers.Contains("语音与截屏"), string.Join(" / ", headers));
            Check("页签含「服务与模型」", headers.Contains("服务与模型"), string.Join(" / ", headers));
            Check("页签含「关于与帮助」", headers.Contains("关于与帮助"), string.Join(" / ", headers));

            var hotkeysTab = tabs.Items.OfType<TabItem>()
                .FirstOrDefault(t => (t.Header as string) == "快捷键");
            Check("「快捷键」页内容为 HotkeysSettingsView",
                hotkeysTab?.Content is HostViews.HotkeysSettingsView
                || FindDescendant<HostViews.HotkeysSettingsView>(hotkeysTab) is not null);
            Check("「快捷键」页包含组合键录制控件",
                FindDescendant<DevEnv.Host.Controls.HotkeyRecorderBox>(hotkeysTab) is not null);

            // 常规页里既有主程序设置，也有模块注入的常规选项
            var general = tabs.Items.OfType<TabItem>()
                .FirstOrDefault(t => (t.Header as string) == "常规");
            Check("常规页已加载 SettingsView",
                general?.Content is HostViews.SettingsView
                || FindDescendant<HostViews.SettingsView>(general) is not null);
            Check("常规页已注入模块常规选项",
                general is not null && FindDescendant<Views.SigXorGeneralView>(general) is not null);

            // 切到「语音与截屏」页，内容能渲染
            var voice = tabs.Items.OfType<TabItem>()
                .FirstOrDefault(t => (t.Header as string) == "语音与截屏");
            tabs.SelectedItem = voice;
            Check("切到「语音与截屏」页签", tabs.SelectedItem == voice);
            Check("该页签内容为 SigXorVoiceView",
                FindDescendant<Views.SigXorVoiceView>(voice) is not null);

            // 切到「服务与模型」页，内容能渲染
            var service = tabs.Items.OfType<TabItem>()
                .FirstOrDefault(t => (t.Header as string) == "服务与模型");
            tabs.SelectedItem = service;
            Check("切到「服务与模型」页签", tabs.SelectedItem == service);
            Check("该页签内容为 SigXorServiceView",
                FindDescendant<Views.SigXorServiceView>(service) is not null);

            // 「关于与帮助」由主程序提供：产品信息、检查更新与文档入口
            var about = tabs.Items.OfType<TabItem>()
                .FirstOrDefault(t => (t.Header as string) == "关于与帮助");
            Check("「关于与帮助」页已加载 AboutHelpView",
                about is not null && FindDescendant<HostViews.AboutHelpView>(about) is not null);
            Check("「关于与帮助」页包含检查更新按钮",
                about is not null
                && FindDescendant<Button>(about) is not null);

            // 显示一遍，触发 Loaded / 布局，捕获静默的 XAML 异常
            window.Show();
            window.UpdateLayout();
            Check("设置窗口可正常显示", window.IsVisible);

            // 五个页签依次切换一遍
            foreach (var header in new[] { "常规", "快捷键", "语音与截屏", "服务与模型", "关于与帮助" })
            {
                var tab = tabs.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == header);
                tabs.SelectedItem = tab;
                window.UpdateLayout();
                Check($"页签「{header}」可正常切换", tabs.SelectedItem == tab);
            }
        }
        catch (Exception ex)
        {
            failures++;
            report.AppendLine($"FAIL  探针异常: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { window?.Close(); } catch { }
        }

        string text = report.ToString();
        ProbeOutput.Line(text);
        ProbeOutput.Line($"失败 {failures} 项 / 共 {failures + CountOk(text)} 项");

        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            try { File.WriteAllText(args[0], ProbeOutput.Flush()); } catch { }
        }

        return failures == 0 ? 0 : 1;
    }

    private static TabControl? FindTabControl(DependencyObject root)
    {
        if (root is TabControl tab) return tab;
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject obj && FindTabControl(obj) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>在视觉树里找某个类型的子元素（用于确认内容确实挂上了）。</summary>
    private static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null) return null;
        if (root is T typed) return typed;
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject obj && FindDescendant<T>(obj) is { } found)
                return found;
        }
        return null;
    }

    private static int CountOk(string text) =>
        text.Split('\n').Count(l => l.StartsWith("OK ", StringComparison.Ordinal));
}
