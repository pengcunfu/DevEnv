using System.Windows.Controls;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>导航 id → 页面实例。本地应用 / 文件夹 / 网站已移到独立的全局启动器口。</summary>
public static class ViewFactory
{
    public static IReadOnlyList<NavItem> NavItems { get; } =
    [
        // Glyph 取自 Segoe MDL2 Assets：终端 / 闪电 / 网络 / 进程 / 服务 / 扳手
        new NavItem { Id = "devenv", Title = "开发环境", Glyph = "\uE756" },
        new NavItem { Id = "lava", Title = "熔岩环境", Glyph = "\uE945" },
        new NavItem { Id = "network", Title = "网络监控", Glyph = "\uE968" },
        new NavItem { Id = "processes", Title = "进程监控", Glyph = "\uE9D9" },
        new NavItem { Id = "services", Title = "服务监控", Glyph = "\uE9F5" },
        new NavItem { Id = "tools", Title = "系统工具", Glyph = "\uE90F" },
    ];

    public static UserControl Create(string id) => id switch
    {
        "devenv" => new DevEnvView(),
        "lava" => new LavaView(),
        "network" => new NetworkView(),
        "processes" => new ProcessesView(),
        "services" => new ServicesView(),
        "tools" => new ToolsView(),
        "translator" => new TranslatorEntryView(),
        "clipboard" => new ClipboardEntryView(),
        _ => new PlaceholderView("未知页面", id),
    };
}
