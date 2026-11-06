using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DevEnv.Core.SigXor.Toolkit;

namespace DevEnv.Host.SigXor.Services;

public static class WindowPlacement
{
    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);

    public static void SetPhysicalBounds(Window window, PixelRect bounds, bool activate = false)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.Left = bounds.X;
            window.Top = bounds.Y;
            window.Width = bounds.Width;
            window.Height = bounds.Height;
            return;
        }

        uint flags = SwpShowWindow | (activate ? 0 : SwpNoActivate);
        SetWindowPos(hwnd, HwndTopmost, bounds.X, bounds.Y, bounds.Width, bounds.Height, flags);
    }

    public static void SetNoActivate(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        long style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style |= WsExNoActivate | WsExToolWindow;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
    }

    /// <summary>
    /// 强制把窗口拉到前台。从托盘 / 全局热键唤起时，后台进程直接 Activate 常被
    /// Windows 焦点规则拒绝，随后 Deactivated 会把启动器立刻关掉。
    /// </summary>
    public static void ForceForeground(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        IntPtr foreground = GetForegroundWindow();
        uint foregroundThread = foreground != IntPtr.Zero
            ? GetWindowThreadProcessId(foreground, out _)
            : 0;
        uint targetThread = GetWindowThreadProcessId(hwnd, out _);
        uint currentThread = GetCurrentThreadId();

        bool attachedFg = false;
        bool attachedCur = false;
        try
        {
            if (foregroundThread != 0 && foregroundThread != targetThread)
                attachedFg = AttachThreadInput(foregroundThread, targetThread, true);
            if (currentThread != targetThread)
                attachedCur = AttachThreadInput(currentThread, targetThread, true);

            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            window.Activate();
        }
        finally
        {
            if (attachedCur)
                AttachThreadInput(currentThread, targetThread, false);
            if (attachedFg)
                AttachThreadInput(foregroundThread, targetThread, false);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
}
