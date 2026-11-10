using System.Windows.Controls;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>导航 id → 页面实例。本地应用 / 文件夹 / 网站 / 系统工具已移到独立的启动器。</summary>
public static class ViewFactory
{
    /// <summary>开发窗口的导航项：开发环境与熔岩环境。</summary>
    public static IReadOnlyList<NavItem> DevelopmentNavItems { get; } =
    [
        // Glyph 取自 Segoe MDL2 Assets：终端 / 闪电
        new NavItem { Id = "devenv", Title = "开发环境", Glyph = "\uE756" },
        new NavItem { Id = "lava", Title = "熔岩环境", Glyph = "\uE945" },
    ];

    /// <summary>监控窗口的导航项：网络 / 进程 / 服务监控。</summary>
    public static IReadOnlyList<NavItem> MonitorNavItems { get; } =
    [
        // Glyph 取自 Segoe MDL2 Assets：网络 / 进程 / 服务
        new NavItem { Id = "network", Title = "网络监控", Glyph = "\uE968" },
        new NavItem { Id = "processes", Title = "进程监控", Glyph = "\uE9D9" },
        new NavItem { Id = "services", Title = "服务监控", Glyph = "\uE9F5" },
    ];

    /// <summary>页面 id 是否属于监控窗口；其余主窗页面归开发窗口。</summary>
    public static bool IsMonitorPage(string? id) => id?.TrimStart('/').ToLowerInvariant() switch
    {
        "network" or "processes" or "services" => true,
        _ => false,
    };

    public static UserControl Create(string id) => id switch
    {
        "devenv" => new DevEnvView(),
        "lava" => new LavaView(),
        "network" => new NetworkView(),
        "processes" => new ProcessesView(),
        "services" => new ServicesView(),
        "translator" => new TranslatorEntryView(),
        "clipboard" => new ClipboardEntryView(),
        _ => new PlaceholderView("未知页面", id),
    };
}
