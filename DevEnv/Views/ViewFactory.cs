using System.Windows.Controls;
using DevEnv.Host.ViewModels;

namespace DevEnv.Host.Views;

/// <summary>导航 id → 页面实例。批次 4~8 逐个把对应分支换成真实视图。</summary>
public static class ViewFactory
{
    public static IReadOnlyList<NavItem> NavItems { get; } =
    [
        new NavItem { Id = "apps", Title = "本地应用" },
        new NavItem { Id = "folders", Title = "文件夹" },
        new NavItem { Id = "webs", Title = "网站" },
        new NavItem { Id = "devenv", Title = "开发环境" },
        new NavItem { Id = "lava", Title = "熔岩环境" },
        new NavItem { Id = "network", Title = "网络监控" },
        new NavItem { Id = "processes", Title = "进程监控" },
        new NavItem { Id = "tools", Title = "系统工具" },
    ];

    public static UserControl Create(string id) => id switch
    {
        "apps" => new AppsView(),
        "folders" => new FoldersView(),
        "webs" => new WebsView(),
        "devenv" => new DevEnvView(),
        "lava" => new LavaView(),
        "network" => new NetworkView(),
        "processes" => new ProcessesView(),
        "tools" => new ToolsView(),
        "translator" => new TranslatorEntryView(),
        "clipboard" => new ClipboardEntryView(),
        _ => new PlaceholderView("未知页面", id),
    };
}