using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DevEnv.Core.SigXor.Toolkit;
using DevEnv.Host.SigXor.Services;
using Forms = System.Windows.Forms;

namespace DevEnv.Host.SigXor.Toolkit.Capture;

public partial class ScreenshotToolbarWindow : Window
{
    private readonly WpfScreenService _screens;
    private readonly DispatcherTimer _toastTimer;
    private PixelRect _anchor;

    public ScreenshotToolbarWindow(WpfScreenService screens)
    {
        _screens = screens;
        InitializeComponent();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            HideToast();
        };
    }

    public event EventHandler? OcrRequested;
    public event EventHandler? ColorPickRequested;
    public event EventHandler? CopyRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? DoneRequested;

    public void ShowAt(PixelRect anchor)
    {
        _anchor = anchor;
        Show();
        Reposition();
    }

    public void MoveNear(PixelRect anchor)
    {
        _anchor = anchor;
        Reposition();
    }

    public void SetOcrEnabled(bool enabled)
    {
        OcrButton.IsEnabled = enabled;
        OcrButton.ToolTip = enabled ? null : "OCR 将在后续批次接入";
    }

    public void SetColorPickEnabled(bool enabled)
    {
        ColorPickButton.IsEnabled = enabled;
        ColorPickButton.ToolTip = enabled ? null : "取色将在后续批次接入";
    }

    public void ShowToast(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void HideToast()
    {
        StatusText.Text = "";
        StatusText.Visibility = Visibility.Collapsed;
    }

    public void SetBusy(string message)
    {
        OcrButton.IsEnabled = false;
        ColorPickButton.IsEnabled = false;
        CopyButton.IsEnabled = false;
        SaveButton.IsEnabled = false;
        DoneButton.IsEnabled = false;
        ShowToast(message);
    }

    public void SetIdle()
    {
        OcrButton.IsEnabled = false;
        ColorPickButton.IsEnabled = false;
        CopyButton.IsEnabled = true;
        SaveButton.IsEnabled = true;
        DoneButton.IsEnabled = true;
        HideToast();
    }

    private void Reposition()
    {
        if (!IsVisible) return;

        // 窗口按内容自适应大小（SizeToContent），这里取实测尺寸换算成物理像素，
        // 避免外壳比按钮内容大出一圈。
        UpdateLayout();
        double dipWidth = ActualWidth > 0 ? ActualWidth : DesiredSize.Width;
        double dipHeight = ActualHeight > 0 ? ActualHeight : DesiredSize.Height;
        if (dipWidth <= 0) dipWidth = 340;
        if (dipHeight <= 0) dipHeight = 48;

        double scale = _screens.GetScaleForPoint(_anchor.X + _anchor.Width / 2, _anchor.Y + _anchor.Height / 2);
        int width = PixelRect.DipToPhysical((int)Math.Ceiling(dipWidth), scale);
        int height = PixelRect.DipToPhysical((int)Math.Ceiling(dipHeight), scale);
        Forms.Screen? screen = _screens.GetScreenForRect(_anchor);
        PixelRect area = screen is null
            ? new PixelRect(0, 0, 1920, 1080)
            : _screens.GetWorkingArea(screen);
        PixelRect placed = PixelRect.PlaceNear(_anchor, area, width, height, gap: 8);
        WindowPlacement.SetPhysicalBounds(this, placed);
    }

    private void OnSurfaceMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        e.Handled = true;
        try
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                DragMove();
                return;
            }
            ReleaseCapture();
            SendMessage(hwnd, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
        }
        catch
        {
        }
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void OnOcrClick(object sender, RoutedEventArgs e) => OcrRequested?.Invoke(this, EventArgs.Empty);
    private void OnColorPickClick(object sender, RoutedEventArgs e) => ColorPickRequested?.Invoke(this, EventArgs.Empty);
    private void OnCopyClick(object sender, RoutedEventArgs e) => CopyRequested?.Invoke(this, EventArgs.Empty);
    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);
    private void OnDoneClick(object sender, RoutedEventArgs e) => DoneRequested?.Invoke(this, EventArgs.Empty);

    protected override void OnClosed(EventArgs e)
    {
        _toastTimer.Stop();
        OcrRequested = null;
        ColorPickRequested = null;
        CopyRequested = null;
        SaveRequested = null;
        DoneRequested = null;
        base.OnClosed(e);
    }

    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
