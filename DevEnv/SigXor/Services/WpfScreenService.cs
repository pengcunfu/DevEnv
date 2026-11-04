using System.Runtime.InteropServices;
using DevEnv.Core.SigXor.Toolkit;
using Forms = System.Windows.Forms;

namespace DevEnv.Host.SigXor.Services;

public sealed class WpfScreenService
{
    public IReadOnlyList<Forms.Screen> Screens => Forms.Screen.AllScreens;

    public PixelRect VirtualBounds
    {
        get
        {
            PixelRect? union = null;
            foreach (Forms.Screen screen in Forms.Screen.AllScreens)
            {
                PixelRect bounds = ToPixelRect(screen.Bounds);
                union = union is null ? bounds : union.Value.Union(bounds);
            }
            return union ?? ToPixelRect(Forms.Screen.PrimaryScreen?.Bounds
                ?? new System.Drawing.Rectangle(0, 0, 1920, 1080));
        }
    }

    public PixelRect GetBounds(Forms.Screen screen) => ToPixelRect(screen.Bounds);

    public PixelRect GetWorkingArea(Forms.Screen screen) => ToPixelRect(screen.WorkingArea);

    public double GetScale(Forms.Screen screen)
    {
        PixelRect bounds = GetBounds(screen);
        return GetScaleForPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    public double GetScaleForPoint(int x, int y)
    {
        try
        {
            IntPtr monitor = MonitorFromPoint(new NativePoint(x, y), MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero) return 1.0;
            if (GetDpiForMonitor(monitor, MdtEffectiveDpi, out uint dpiX, out _) != 0 || dpiX == 0)
                return 1.0;
            return dpiX / 96.0;
        }
        catch
        {
            return 1.0;
        }
    }

    public Forms.Screen? GetScreenForPoint(int x, int y)
    {
        foreach (Forms.Screen screen in Forms.Screen.AllScreens)
        {
            PixelRect bounds = GetBounds(screen);
            if (bounds.Contains(x, y)) return screen;
        }
        return Forms.Screen.PrimaryScreen;
    }

    public Forms.Screen? GetScreenForRect(PixelRect rect)
    {
        int x = rect.X + rect.Width / 2;
        int y = rect.Y + rect.Height / 2;
        Forms.Screen? byCenter = GetScreenForPoint(x, y);
        if (byCenter is not null) return byCenter;

        foreach (Forms.Screen screen in Forms.Screen.AllScreens)
        {
            if (GetBounds(screen).Intersects(rect)) return screen;
        }
        return Forms.Screen.PrimaryScreen;
    }

    public PixelRect PlaceNear(PixelRect anchor, int width, int height, int gap = 8)
    {
        Forms.Screen screen = GetScreenForRect(anchor) ?? Forms.Screen.PrimaryScreen!;
        return PixelRect.PlaceNear(anchor, GetWorkingArea(screen), width, height, gap);
    }

    public static PixelRect ToPixelRect(System.Drawing.Rectangle rect) =>
        new(rect.X, rect.Y, rect.Width, rect.Height);

    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;

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
    private static extern IntPtr MonitorFromPoint(NativePoint pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
