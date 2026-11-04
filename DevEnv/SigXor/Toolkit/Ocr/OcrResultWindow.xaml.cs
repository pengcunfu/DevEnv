using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DevEnv.Core.SigXor.Toolkit;
using DevEnv.Host.SigXor.Services;
using Forms = System.Windows.Forms;

namespace DevEnv.Host.SigXor.Toolkit.Ocr;

public partial class OcrResultWindow : Window
{
    private readonly WpfScreenService _screens;
    private PixelRect _anchor;
    private bool _hasAnchor;

    public OcrResultWindow(WpfScreenService screens)
    {
        _screens = screens;
        InitializeComponent();
        Loaded += (_, _) => Reposition();
        SourceInitialized += (_, _) => Reposition();
    }

    public void ShowBusy(string message, PixelRect anchor)
    {
        _anchor = anchor;
        _hasAnchor = true;
        CopyButton.IsEnabled = false;
        ResultText.Text = "";
        MetaText.Text = "";
        SetStatus(message, success: false);
        Show();
        Reposition();
        // 截图预览窗同为 Topmost；普通 Activate 不保证盖过它，这里显式抬到最前
        WindowPlacement.ForceForeground(this);
        Focus();
    }

    public void SetBusy(string message) => SetStatus(message, success: false);

    /// <summary>截图预览被拖动时，结果窗跟随新的选区位置。</summary>
    public void MoveTo(PixelRect anchor)
    {
        _anchor = anchor;
        _hasAnchor = true;
        Reposition();
    }

    public void ShowResult(string text)
    {
        string content = text ?? "";
        SetStatus("识别完成", success: true);
        ResultText.Text = content;
        CopyButton.IsEnabled = !string.IsNullOrWhiteSpace(content);
        MetaText.Text = BuildMeta(content);
        Reposition();
    }

    public void ShowFailure(string message)
    {
        SetStatus(message, success: false);
        CopyButton.IsEnabled = false;
        MetaText.Text = "";
        Reposition();
    }

    private void SetStatus(string message, bool success)
    {
        StatusText.Text = message;
        StatusText.Foreground = success
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F6B4F"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5B6577"));
        StatusBadge.Background = success
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E7F5EE"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDF2F7"));
    }

    private static string BuildMeta(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        int lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        int chars = normalized.Replace("\n", "").Length;
        return $"{chars} 字 · {lines} 行";
    }

    private void Reposition()
    {
        UpdateLayout();

        double scale = _screens.GetScaleForPoint(
            _hasAnchor ? _anchor.X + _anchor.Width / 2 : 0,
            _hasAnchor ? _anchor.Y + _anchor.Height / 2 : 0);
        int width = (int)Math.Ceiling(Width * scale);
        int height = (int)Math.Ceiling(Height * scale);
        if (width <= 0) width = 600;
        if (height <= 0) height = 380;

        Forms.Screen? screen = _hasAnchor ? _screens.GetScreenForRect(_anchor) : Forms.Screen.PrimaryScreen;
        PixelRect area = screen is null
            ? new PixelRect(0, 0, 1920, 1080)
            : _screens.GetWorkingArea(screen);

        int x = _hasAnchor ? _anchor.X + (_anchor.Width - width) / 2 : area.X + (area.Width - width) / 2;
        int y = _hasAnchor ? _anchor.Y + (_anchor.Height - height) / 2 : area.Y + (area.Height - height) / 2;
        x = Math.Clamp(x, area.X + 8, Math.Max(area.X + 8, area.Right - width - 8));
        y = Math.Clamp(y, area.Y + 8, Math.Max(area.Y + 8, area.Bottom - height - 8));
        WindowPlacement.SetPhysicalBounds(this, new PixelRect(x, y, width, height));
    }

    private void OnTitleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        e.Handled = true;
        try
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
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
            if (source is System.Windows.Controls.Button) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        string text = ResultText.Text;
        if (string.IsNullOrEmpty(text))
        {
            Close();
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            CopyButton.Content = "复制失败";
            CopyButton.IsEnabled = false;
            return;
        }

        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
