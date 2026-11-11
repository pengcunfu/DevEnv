using System.Windows.Controls;

namespace DevEnv.Host.Views;

/// <summary>导航 / 工具 id → 页面实例。功能页一律走独立工具窗，不再嵌进复合主窗。</summary>
public static class ViewFactory
{
    public static UserControl Create(string id) => id switch
    {
        "devenv" => new DevEnvView(),
        "lava" => new LavaView(),
        "network" => new NetworkView(),
        "processes" => new ProcessesView(),
        "services" => new ServicesView(),
        "ports" => new PortsView(),
        "speed" => new SpeedTestView(),
        "translator" => new TranslatorEntryView(),
        "clipboard" => new ClipboardEntryView(),
        _ => new PlaceholderView("未知页面", id),
    };

    /// <summary>是否为启动器页签 id（apps / folders / webs / …）。</summary>
    public static bool IsLauncherPage(string? id)
    {
        var normalized = id?.TrimStart('/').ToLowerInvariant();
        return normalized is "apps" or "folders" or "webs" or "startmenu" or "tools" or "builtin";
    }
}
