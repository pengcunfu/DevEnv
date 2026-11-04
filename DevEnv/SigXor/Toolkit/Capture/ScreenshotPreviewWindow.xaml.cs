using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DevEnv.Core.SigXor.Toolkit;
using DevEnv.Host.SigXor.Services;

namespace DevEnv.Host.SigXor.Toolkit.Capture;

/// <summary>
/// 截图预览窗：按物理像素 1:1 显示选区，可拖动；取色模式下直接在预览图上放大取色。
/// </summary>
public partial class ScreenshotPreviewWindow : Window
{
    private const int SampleRadius = 7;
    private const int CellSize = 10;
    private const int MagnifierSize = (SampleRadius * 2 + 1) * CellSize;

    private readonly WriteableBitmap _magnifier;
    private readonly DispatcherTimer _flashTimer;
    private BitmapSource? _image;
    private PixelRect _screenRect;
    private double _scale = 1.0;
    private bool _dragging;
    private NativePoint _dragStartCursor;
    private PixelRect _dragStartRect;
    private bool _colorPicking;
    private bool _cursorInside;
    private int _cursorX;
    private int _cursorY;
    private byte _red;
    private byte _green;
    private byte _blue;
    private bool _useHex = true;
    private bool _copiedFlash;

    public ScreenshotPreviewWindow()
    {
        InitializeComponent();
        _magnifier = new WriteableBitmap(
            MagnifierSize, MagnifierSize, 96, 96, PixelFormats.Bgra32, null);
        MagnifierImage.Source = _magnifier;
        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        _flashTimer.Tick += (_, _) =>
        {
            _flashTimer.Stop();
            _copiedFlash = false;
            UpdateLabels();
        };
        SourceInitialized += (_, _) => WindowPlacement.SetPhysicalBounds(this, _screenRect);
    }

    public event EventHandler<PixelRect>? DragPositionChanged;
    public event EventHandler<string>? ColorCopied;
    public event EventHandler? ColorPickEnded;

    public bool IsColorPicking => _colorPicking;

    public void ShowAt(BitmapSource image, PixelRect screenRect, double scale)
    {
        _image = image;
        PreviewImage.Source = image;
        _screenRect = screenRect;
        _scale = scale > 0 ? scale : 1.0;
        Width = screenRect.Width / _scale;
        Height = screenRect.Height / _scale;
        Show();
        WindowPlacement.SetPhysicalBounds(this, screenRect);
    }

    /// <summary>进入取色模式：预览保持可见，在图片像素上直接取样。</summary>
    public void StartColorPick()
    {
        if (_image is null || _colorPicking) return;

        _colorPicking = true;
        _dragging = false;
        if (DragSurface.IsMouseCaptured) DragSurface.ReleaseMouseCapture();

        PickOverlay.Visibility = Visibility.Visible;
        DragSurface.Cursor = Cursors.Cross;
        Activate();
        DragSurface.Focus();
        DragSurface.CaptureMouse();

        GetCursorPos(out NativePoint point);
        UpdateSampleFromScreen(point.X, point.Y);
    }

    /// <summary>退出取色模式，恢复拖动。</summary>
    public void StopColorPick()
    {
        if (!_colorPicking) return;

        _colorPicking = false;
        _flashTimer.Stop();
        _copiedFlash = false;
        PickOverlay.Visibility = Visibility.Collapsed;
        DragSurface.Cursor = Cursors.SizeAll;
        if (DragSurface.IsMouseCaptured) DragSurface.ReleaseMouseCapture();
        ColorPickEnded?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        if (_colorPicking)
        {
            CopyCurrentColor();
            e.Handled = true;
            return;
        }

        GetCursorPos(out _dragStartCursor);
        _dragStartRect = _screenRect;
        _dragging = true;
        DragSurface.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_colorPicking)
        {
            GetCursorPos(out NativePoint point);
            UpdateSampleFromScreen(point.X, point.Y);
            e.Handled = true;
            return;
        }

        if (!_dragging) return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag();
            return;
        }

        GetCursorPos(out NativePoint current);
        int dx = current.X - _dragStartCursor.X;
        int dy = current.Y - _dragStartCursor.Y;
        _screenRect = new PixelRect(
            _dragStartRect.X + dx, _dragStartRect.Y + dy,
            _dragStartRect.Width, _dragStartRect.Height);
        WindowPlacement.SetPhysicalBounds(this, _screenRect);
        DragPositionChanged?.Invoke(this, _screenRect);
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_colorPicking)
        {
            e.Handled = true;
            return;
        }
        if (!_dragging) return;
        EndDrag();
        e.Handled = true;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_colorPicking) return;
        StopColorPick();
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_colorPicking) return;

        switch (e.Key)
        {
            case Key.Escape:
                StopColorPick();
                e.Handled = true;
                break;
            case Key.C:
                CopyCurrentColor();
                e.Handled = true;
                break;
            case Key.LeftShift:
            case Key.RightShift:
                _useHex = !_useHex;
                UpdateLabels();
                e.Handled = true;
                break;
            case Key.W:
            case Key.Up:
                Nudge(0, -1);
                e.Handled = true;
                break;
            case Key.S:
            case Key.Down:
                Nudge(0, 1);
                e.Handled = true;
                break;
            case Key.A:
            case Key.Left:
                Nudge(-1, 0);
                e.Handled = true;
                break;
            case Key.D:
            case Key.Right:
                Nudge(1, 0);
                e.Handled = true;
                break;
        }
    }

    private void EndDrag()
    {
        _dragging = false;
        DragSurface.ReleaseMouseCapture();
        DragPositionChanged?.Invoke(this, _screenRect);
    }

    private void UpdateSampleFromScreen(int screenX, int screenY)
    {
        if (_image is null) return;

        int rawX = screenX - _screenRect.X;
        int rawY = screenY - _screenRect.Y;
        _cursorInside = rawX >= 0 && rawY >= 0
            && rawX < _image.PixelWidth && rawY < _image.PixelHeight;
        _cursorX = Math.Clamp(rawX, 0, Math.Max(0, _image.PixelWidth - 1));
        _cursorY = Math.Clamp(rawY, 0, Math.Max(0, _image.PixelHeight - 1));

        RefreshSample();
        PositionMagnifier(screenX, screenY);
    }

    private void Nudge(int dx, int dy)
    {
        if (_image is null) return;

        _cursorX = Math.Clamp(_cursorX + dx, 0, Math.Max(0, _image.PixelWidth - 1));
        _cursorY = Math.Clamp(_cursorY + dy, 0, Math.Max(0, _image.PixelHeight - 1));
        _cursorInside = true;
        RefreshSample();
        PositionMagnifier(_screenRect.X + _cursorX, _screenRect.Y + _cursorY);
    }

    private void RefreshSample()
    {
        if (_image is null) return;

        SamplePixel(_cursorX, _cursorY, out _red, out _green, out _blue);
        RenderMagnifier(_cursorX, _cursorY);
        ColorSwatch.Background = new SolidColorBrush(Color.FromRgb(_red, _green, _blue));
        UpdateLabels();
    }

    private void SamplePixel(int x, int y, out byte red, out byte green, out byte blue)
    {
        var pixel = new byte[4];
        _image!.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        blue = pixel[0];
        green = pixel[1];
        red = pixel[2];
    }

    private void RenderMagnifier(int centerX, int centerY)
    {
        if (_image is null) return;

        int sourceWidth = _image.PixelWidth;
        int sourceHeight = _image.PixelHeight;
        int cx = Math.Clamp(centerX, SampleRadius, Math.Max(SampleRadius, sourceWidth - 1 - SampleRadius));
        int cy = Math.Clamp(centerY, SampleRadius, Math.Max(SampleRadius, sourceHeight - 1 - SampleRadius));

        int sourceStride = (SampleRadius * 2 + 1) * 4;
        var source = new byte[sourceStride * (SampleRadius * 2 + 1)];
        var destination = new byte[MagnifierSize * MagnifierSize * 4];
        int destStride = MagnifierSize * 4;

        _image.CopyPixels(
            new Int32Rect(cx - SampleRadius, cy - SampleRadius, SampleRadius * 2 + 1, SampleRadius * 2 + 1),
            source, sourceStride, 0);

        for (int gy = 0; gy < SampleRadius * 2 + 1; gy++)
        {
            for (int gx = 0; gx < SampleRadius * 2 + 1; gx++)
            {
                int sourceOffset = gy * sourceStride + gx * 4;
                byte b = source[sourceOffset];
                byte g = source[sourceOffset + 1];
                byte r = source[sourceOffset + 2];

                int dx0 = gx * CellSize;
                int dy0 = gy * CellSize;
                for (int py = 0; py < CellSize; py++)
                {
                    for (int px = 0; px < CellSize; px++)
                    {
                        bool edge = px == 0 || py == 0;
                        int offset = (dy0 + py) * destStride + (dx0 + px) * 4;
                        destination[offset] = edge ? (byte)Math.Max(0, b - 24) : b;
                        destination[offset + 1] = edge ? (byte)Math.Max(0, g - 24) : g;
                        destination[offset + 2] = edge ? (byte)Math.Max(0, r - 24) : r;
                        destination[offset + 3] = 255;
                    }
                }
            }
        }

        _magnifier.WritePixels(
            new Int32Rect(0, 0, MagnifierSize, MagnifierSize),
            destination, destStride, 0);
    }

    private void PositionMagnifier(int screenX, int screenY)
    {
        double scale = _scale <= 0 ? 1.0 : _scale;
        double cursorX = (screenX - _screenRect.X) / scale;
        double cursorY = (screenY - _screenRect.Y) / scale;
        double cardWidth = MagnifierCard.ActualWidth > 0 ? MagnifierCard.ActualWidth : 184;
        double cardHeight = MagnifierCard.ActualHeight > 0 ? MagnifierCard.ActualHeight : 224;
        double canvasWidth = PickOverlay.ActualWidth;
        double canvasHeight = PickOverlay.ActualHeight;
        if (canvasWidth <= 0) canvasWidth = ActualWidth;
        if (canvasHeight <= 0) canvasHeight = ActualHeight;

        double x = cursorX + 24;
        double y = cursorY + 24;
        if (x + cardWidth > canvasWidth - 8) x = cursorX - cardWidth - 24;
        if (y + cardHeight > canvasHeight - 8) y = cursorY - cardHeight - 24;
        x = Math.Clamp(x, 8, Math.Max(8, canvasWidth - cardWidth - 8));
        y = Math.Clamp(y, 8, Math.Max(8, canvasHeight - cardHeight - 8));
        Canvas.SetLeft(MagnifierCard, x);
        Canvas.SetTop(MagnifierCard, y);
    }

    private void CopyCurrentColor()
    {
        if (!_cursorInside) return;

        string value = FormatColor(_red, _green, _blue, _useHex);
        try
        {
            Clipboard.SetText(value);
        }
        catch
        {
            HintText.Text = "复制失败";
            return;
        }

        _copiedFlash = true;
        UpdateLabels();
        _flashTimer.Stop();
        _flashTimer.Start();
        ColorCopied?.Invoke(this, value);
    }

    private void UpdateLabels()
    {
        if (!_cursorInside)
        {
            ColorValueText.Text = "—";
            HintText.Text = "移到预览图内取色 · Esc 退出";
            return;
        }

        string value = FormatColor(_red, _green, _blue, _useHex);
        ColorValueText.Text = _copiedFlash ? $"已复制 {value}" : value;
        HintText.Text = _useHex
            ? "点击/C 复制 · Shift→RGB · Esc 退出"
            : "点击/C 复制 · Shift→HEX · Esc 退出";
    }

    private static string FormatColor(byte r, byte g, byte b, bool hex) =>
        hex ? $"#{r:X2}{g:X2}{b:X2}" : $"RGB({r}, {g}, {b})";

    protected override void OnClosed(EventArgs e)
    {
        _flashTimer.Stop();
        DragPositionChanged = null;
        ColorCopied = null;
        ColorPickEnded = null;
        base.OnClosed(e);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
