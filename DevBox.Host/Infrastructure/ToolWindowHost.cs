using System.Windows;
using System.Windows.Controls;
using DevBox.Host.Views;
using DevBox.Host.Windows;

namespace DevBox.Host.Infrastructure;

/// <summary>
/// 工具窗口宿主：每个工具只保留一个实例，重复点菜单是「前置已有窗口」而不是开一堆。
/// 工具一律用独立非模态窗口，不再塞进主窗内容区。
/// </summary>
public static class ToolWindowHost
{
    private static readonly Dictionary<string, ToolWindow> Windows = new();

    /// <summary>工具 id → (标题, 视图工厂)。</summary>
    private static readonly Dictionary<string, (string Title, Func<UserControl> Factory)> Tools = new()
    {
        ["json"] = ("JSON 格式化器", () => new JsonToolView()),
        ["hosts"] = ("Hosts 文件编辑器", () => new HostsToolView()),
        ["hash"] = ("文件哈希计算器", () => new HashToolView()),
        ["image"] = ("图像格式转换器", () => new ImageToolView()),
        ["startup"] = ("启动项管理", () => new StartupToolView()),
        ["context-menu"] = ("右键菜单管理", () => new ContextMenuView()),
    };

    /// <summary>支持的工具 id。</summary>
    public static bool IsTool(string id) => Tools.ContainsKey(id);

    public static IReadOnlyList<string> ToolIds => Tools.Keys.ToList();

    /// <summary>打开（或前置）指定工具窗口。</summary>
    public static void Open(string id)
    {
        if (!Tools.TryGetValue(id, out var tool))
            return;

        var window = Windows.GetValueOrDefault(id);
        if (window is null)
        {
            window = new ToolWindow(tool.Title, tool.Factory());
            window.Closed += (_, _) => Windows.Remove(id);
            Windows[id] = window;
        }

        AttachOwner(window);

        if (window.IsVisible)
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.Activate();
            return;
        }

        window.Show();
        window.Activate();
    }

    /// <summary>主窗恢复显示时，把已打开的工具窗一起带出来。</summary>
    public static void RefreshAll()
    {
        foreach (var window in Windows.Values)
            AttachOwner(window);
    }

    /// <summary>主窗真正退出前调用，避免 owned 窗口被 Cancel 影响退出流程。</summary>
    public static void CloseAll()
    {
        foreach (var window in Windows.Values.ToList())
        {
            try
            {
                window.Close();
            }
            catch
            {
                // 已在退出路径，忽略
            }
        }
        Windows.Clear();
    }

    private static void AttachOwner(Window window)
    {
        var main = Application.Current?.MainWindow;
        if (main is not null && !ReferenceEquals(main, window) && window.Owner != main)
            window.Owner = main;
    }
}