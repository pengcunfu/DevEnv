using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DevEnv.Core.SigXor.Toolkit;

namespace DevEnv.Host.SigXor.Toolkit.Capture;

public static class ScreenCaptureService
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint DibRgbColors = 0;
    private const uint BiRgb = 0;
    private const uint SrcCopy = 0x00CC0020;

    public static BitmapSource? CaptureVirtualDesktop()
    {
        int x = GetSystemMetrics(SmXVirtualScreen);
        int y = GetSystemMetrics(SmYVirtualScreen);
        int width = GetSystemMetrics(SmCxVirtualScreen);
        int height = GetSystemMetrics(SmCyVirtualScreen);
        if (width <= 0 || height <= 0) return null;

        using var capture = CaptureToDib(x, y, width, height);
        if (capture is null) return null;

        var pixels = new byte[capture.Stride * capture.Height];
        Marshal.Copy(capture.BitsPtr, pixels, 0, pixels.Length);

        BitmapSource source = BitmapSource.Create(
            capture.Width, capture.Height, 96, 96,
            PixelFormats.Bgra32, null, pixels, capture.Stride);
        source.Freeze();
        return source;
    }

    public static BitmapSource? Crop(BitmapSource source, PixelRect rect)
    {
        var bounds = new PixelRect(0, 0, source.PixelWidth, source.PixelHeight);
        PixelRect clipped = rect.Intersect(bounds);
        if (clipped.IsEmpty) return null;

        var cropped = new CroppedBitmap(
            source, new Int32Rect(clipped.X, clipped.Y, clipped.Width, clipped.Height));
        cropped.Freeze();
        return cropped;
    }

    public static bool CopyToClipboard(BitmapSource source)
    {
        try
        {
            Clipboard.SetImage(source);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SavePng(BitmapSource source, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }

    private static ScreenCapture? CaptureToDib(int x, int y, int width, int height)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) return null;

        IntPtr memDc = CreateCompatibleDC(screenDc);
        if (memDc == IntPtr.Zero)
        {
            ReleaseDC(IntPtr.Zero, screenDc);
            return null;
        }

        var header = new BitmapInfoHeader
        {
            biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            biWidth = width,
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = BiRgb,
        };

        IntPtr hBitmap = CreateDIBSection(
            screenDc, ref header, DibRgbColors, out IntPtr bits, IntPtr.Zero, 0);
        if (hBitmap == IntPtr.Zero || bits == IntPtr.Zero)
        {
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
            return null;
        }

        try
        {
            IntPtr oldObject = SelectObject(memDc, hBitmap);
            try
            {
                if (!BitBlt(memDc, 0, 0, width, height, screenDc, x, y, SrcCopy))
                {
                    DeleteObject(hBitmap);
                    return null;
                }
            }
            finally
            {
                SelectObject(memDc, oldObject);
            }

            return new ScreenCapture(hBitmap, bits, width, height, width * 4);
        }
        finally
        {
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private sealed class ScreenCapture : IDisposable
    {
        public IntPtr HBitmap;
        public readonly IntPtr BitsPtr;
        public readonly int Width;
        public readonly int Height;
        public readonly int Stride;

        public ScreenCapture(IntPtr hBitmap, IntPtr bitsPtr, int width, int height, int stride)
        {
            HBitmap = hBitmap;
            BitsPtr = bitsPtr;
            Width = width;
            Height = height;
            Stride = stride;
        }

        public void Dispose()
        {
            if (HBitmap == IntPtr.Zero) return;
            DeleteObject(HBitmap);
            HBitmap = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc, ref BitmapInfoHeader header, uint usage,
        out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);
}
