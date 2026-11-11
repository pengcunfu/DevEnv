using System.Windows;
using System.Windows.Controls;
using DevEnv.Core.LavaEnv.Services;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Views;
using DevEnv.Host.Windows;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 工具窗口宿主：每个工具只保留一个实例，重复点菜单是「前置已有窗口」而不是开一堆。
/// 工具一律用独立非模态窗口，不再塞进主窗内容区。
/// </summary>
public static class ToolWindowHost
{
    private static readonly Dictionary<string, ToolWindow> Windows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>工具 id → (标题, 视图工厂)。</summary>
    private static readonly Dictionary<string, (string Title, Func<UserControl> Factory)> Tools = new(StringComparer.OrdinalIgnoreCase)
    {
        ["devenv"] = ("开发环境", () => new DevEnvView()),
        ["lava"] = ("熔岩环境", () => new LavaView()),
        ["network"] = ("网络监控", () => new NetworkView()),
        ["processes"] = ("进程监控", () => new ProcessesView()),
        ["services"] = ("服务监控", () => new ServicesView()),
        ["ports"] = ("端口管理", () => new PortsView()),
        ["speed"] = ("网络测速", () => new SpeedTestView()),
        ["json"] = ("JSON 格式化器", () => new JsonToolView()),
        ["hosts"] = ("Hosts 文件编辑器", () => new HostsToolView()),
        ["hash"] = ("文件哈希计算器", () => new HashToolView()),
        ["codec"] = ("编码工具箱", () => new CodecToolView()),
        ["file-tools"] = ("文件解锁与粉碎", () => new FileToolView()),
        ["image"] = ("图像格式转换器", () => new ImageToolView()),
        ["startup"] = ("启动项管理", () => new StartupToolView()),
        ["context-menu"] = ("右键菜单管理", () => new ContextMenuView()),
    };

    /// <summary>支持的工具 id。</summary>
    public static bool IsTool(string id) => Tools.ContainsKey(id);

    public static IReadOnlyList<string> ToolIds => Tools.Keys.ToList();

    /// <summary>按 id 查找已打开的工具窗（状态栏路由用）。</summary>
    public static ToolWindow? Find(string id) =>
        Windows.TryGetValue(id, out var window) ? window : null;

    /// <summary>打开（或前置）指定工具窗口。</summary>
    public static OperationResult Open(string id)
    {
        if (!Tools.TryGetValue(id, out var tool))
            return OperationResult.Fail($"未知工具：{id}");

        // Hosts 编辑器需要写系统目录：普通权限下直接请求管理员权限并拉起
        // 独立编辑窗口，不再展示只读窗口，避免编辑完才发现无法保存。
        if (string.Equals(id, "hosts", StringComparison.OrdinalIgnoreCase)
            && !ElevationHelper.IsAdministrator())
        {
            return CoreServices.Utility.ElevateHosts();
        }

        var window = Windows.GetValueOrDefault(id);
        if (window is null)
        {
            window = new ToolWindow(id, tool.Title, tool.Factory());
            window.Closed += (_, _) => Windows.Remove(id);
            Windows[id] = window;
        }

        AttachOwner(window);

        if (window.IsVisible)
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.Activate();
            return OperationResult.Success();
        }

        // 多屏：跟鼠标所在屏居中，不跟主屏 / Owner 屏
        CursorScreen.Center(window);
        window.Show();
        window.Activate();
        return OperationResult.Success();
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
