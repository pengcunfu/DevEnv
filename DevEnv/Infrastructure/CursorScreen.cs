using System.Runtime.InteropServices;
using System.Windows;
using DevEnv.Core.Services;
using Forms = System.Windows.Forms;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 把窗口放到鼠标所在显示器的工作区中心。
/// 多屏时 <see cref="SystemParameters.WorkArea"/> 只描述主屏，热键 / 托盘唤起必须跟光标走。
/// </summary>
public static class CursorScreen
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;

    /// <summary>鼠标所在屏的工作区（DIP，已扣任务栏）。</summary>
    public static LauncherGeometry.Bounds GetWorkAreaDip()
    {
        GetCursorPos(out var point);
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y))
            ?? Forms.Screen.PrimaryScreen
            ?? Forms.Screen.AllScreens.FirstOrDefault();
        if (screen is null)
        {
            var fallback = SystemParameters.WorkArea;
            return new LauncherGeometry.Bounds(fallback.Left, fallback.Top, fallback.Width, fallback.Height);
        }

        var area = screen.WorkingArea;
        var scale = GetScaleForPoint(area.Left + area.Width / 2, area.Top + area.Height / 2);
        return new LauncherGeometry.Bounds(
            area.Left / scale,
            area.Top / scale,
            area.Width / scale,
            area.Height / scale);
    }

    /// <summary>
    /// 固定启动器尺寸，居中到鼠标所在屏（与 <see cref="LauncherGeometry.Compute"/> 一致）。
    /// </summary>
    public static Rect GetLauncherBoundsDip()
    {
        var area = GetWorkAreaDip();
        var bounds = LauncherGeometry.Compute(area.Left, area.Top, area.Width, area.Height);
        return new Rect(
            Math.Round(bounds.Left),
            Math.Round(bounds.Top),
            Math.Round(bounds.Width),
            Math.Round(bounds.Height));
    }

    /// <summary>
    /// 把窗口摆到鼠标所在屏居中。仅在即将显示（或尚未可见）时调用；
    /// 已可见窗口不要挪，避免用户拖到别处后又被拽回去。
    /// </summary>
    public static void Center(Window window, double? width = null, double? height = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        var w = ResolveSize(width ?? window.Width, window.ActualWidth, LauncherGeometry.Width);
        var h = ResolveSize(height ?? window.Height, window.ActualHeight, LauncherGeometry.Height);
        var area = GetWorkAreaDip();

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Width = w;
        window.Height = h;
        window.Left = Math.Round(area.Left + (area.Width - w) / 2);
        window.Top = Math.Round(area.Top + (area.Height - h) / 2);
    }

    /// <summary>窗口尚未显示时再居中到光标屏；已显示则不动。</summary>
    public static void CenterIfHidden(Window window, double? width = null, double? height = null)
    {
        if (window.IsVisible && window.WindowState != WindowState.Minimized)
            return;
        Center(window, width, height);
    }

    private static double ResolveSize(double preferred, double actual, double fallback)
    {
        if (!double.IsNaN(preferred) && !double.IsInfinity(preferred) && preferred > 0)
            return preferred;
        if (!double.IsNaN(actual) && !double.IsInfinity(actual) && actual > 0)
            return actual;
        return fallback;
    }

    private static double GetScaleForPoint(int x, int y)
    {
        try
        {
            var monitor = MonitorFromPoint(new NativePoint(x, y), MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
                return 1.0;
            if (GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) != 0 || dpiX == 0)
                return 1.0;
            return dpiX / 96.0;
        }
        catch
        {
            return 1.0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;

        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
