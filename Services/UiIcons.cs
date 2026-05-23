using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DevEnv.Services;

public static class UiIcons
{
    private const uint ShgsiIcon = 0x000000100;
    private const uint ShgsiSmallIcon = 0x000000001;
    private const uint SiidShield = 77;

    public static ImageSource UacShield { get; } = LoadUacShield() ?? FallbackShield();

    [DllImport("shell32.dll")]
    private static extern int SHGetStockIconInfo(uint siid, uint uFlags, ref StockIconInfo psii);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StockIconInfo
    {
        public uint cbSize;
        public IntPtr hIcon;
        public int iSysImageIndex;
        public int iIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPath;
    }

    private static ImageSource? LoadUacShield()
    {
        var info = new StockIconInfo { cbSize = (uint)Marshal.SizeOf<StockIconInfo>() };
        if (SHGetStockIconInfo(SiidShield, ShgsiIcon | ShgsiSmallIcon, ref info) != 0
            || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16));
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static ImageSource FallbackShield()
    {
        var visual = new System.Windows.Controls.TextBlock
        {
            Text = "\uE9EA",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Foreground = Brushes.DodgerBlue,
        };
        visual.Measure(new Size(16, 16));
        visual.Arrange(new Rect(0, 0, 16, 16));

        var bitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
