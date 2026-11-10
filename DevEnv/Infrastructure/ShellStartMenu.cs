using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// Windows 开始菜单的识别与收起。开始菜单和启动器一样是置顶窗口：
/// 两者同时存在时谁在上层取决于激活顺序，不显式处理就会出现
/// 「启动器铺满屏幕，开始菜单仍压在上面」的情况。
///
/// 开始菜单的宿主进程随 Windows 版本变化：Windows 10 / 早期 Windows 11 是
/// <c>StartMenuExperienceHost</c>；较新的 Windows 11（实测 build 26200）把它交给
/// <c>SearchHost</c> 承载，窗口类仍是 <c>Windows.UI.Core.CoreWindow</c>，标题是「搜索」。
/// 只认前者会导致新版系统上永远检测不到开始菜单。
/// </summary>
internal static class ShellStartMenu
{
    private const string StartMenuHostProcessName = "StartMenuExperienceHost";
    private const string SearchHostProcessName = "SearchHost";
    private const int VkEscape = 0x1B;

    private static bool IsHostProcessName(string name) =>
        string.Equals(name, StartMenuHostProcessName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, SearchHostProcessName, StringComparison.OrdinalIgnoreCase);

    /// <summary>开始菜单当前是否可见；返回它的顶层窗口句柄。</summary>
    public static bool IsOpen(out IntPtr handle)
    {
        handle = IntPtr.Zero;

        // 正常按 Win 打开时开始菜单就在前台，先查前台最快
        var foreground = GetForegroundWindow();
        if (IsHostWindow(foreground))
        {
            handle = foreground;
            return true;
        }

        // 启动器已经抢到前台时开始菜单会留在下层，仍然要能找到它
        var found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (!IsHostWindow(hwnd))
                return true;

            found = hwnd;
            return false;
        }, IntPtr.Zero);

        handle = found;
        return found != IntPtr.Zero;
    }

    /// <summary>
    /// 开始菜单开着时让它收到一次 Esc；返回是否发送了收起动作。
    /// 调用方应先等开始菜单真正隐藏，再显示自己的置顶窗口。
    /// </summary>
    public static bool DismissIfOpen()
    {
        if (!IsOpen(out var handle))
            return false;

        // 开始菜单不在前台时（例如启动器已经抢到前台），先把它拉回焦点，Esc 才会被它处理
        if (GetForegroundWindow() != handle)
            SetForegroundWindow(handle);

        KeyboardInputSender.Tap(VkEscape);
        return true;
    }

    /// <summary>
    /// 开始菜单的顶层窗口是 CoreWindow（Windows 10 / 11 都是），先按窗口类过滤，
    /// 避免每次打开启动器都给几十个顶层窗口做进程查询。进程名再确认一次：
    /// 老版本是 StartMenuExperienceHost，较新的 Windows 11 是 SearchHost。
    /// </summary>
    private static bool IsHostWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd))
            return false;

        var className = new StringBuilder(256);
        if (GetClassName(hwnd, className, className.Capacity) <= 0)
            return false;

        var name = className.ToString();
        if (!name.Contains("CoreWindow", StringComparison.Ordinal)
            && !name.Contains("IslandWindow", StringComparison.Ordinal))
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return false;

        try
        {
            using var process = Process.GetProcessById(pid);
            return IsHostProcessName(process.ProcessName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // 进程刚好退出或被系统保护，忽略
            return false;
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}
