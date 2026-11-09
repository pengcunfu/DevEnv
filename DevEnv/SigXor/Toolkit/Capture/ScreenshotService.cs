using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using DevEnv.Core.Components;
using DevEnv.Core.SigXor.Toolkit;
using CoreServices = DevEnv.Core.Services.CoreServices;
using DevEnv.Host.SigXor.Services;
using DevEnv.Host.SigXor.Toolkit.Ocr;
using DevEnv.Host.SigXor.Toolkit.Voice;

namespace DevEnv.Host.SigXor.Toolkit.Capture;

public sealed class ScreenshotService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly WpfScreenService _screens;
    private readonly Dispatcher _dispatcher;
    private readonly IDisposable _hotkeyRegistration;
    private WindowVisibilityScope? _visibility;
    private RegionSelectorWindow? _selector;
    private ScreenshotPreviewWindow? _preview;
    private ScreenshotToolbarWindow? _toolbar;
    private OcrResultWindow? _ocrWindow;
    private OcrEngine? _ocrEngine;
    private string? _lastPickedColor;
    private BitmapSource? _fullImage;
    private BitmapSource? _regionImage;
    private PixelRect _virtualBounds;
    private bool _ocrBusy;
    private PixelRect _selectionScreenRect;
    private bool _isCapturing;
    private bool _closing;
    private bool _disposed;

    public ScreenshotService(
        SettingsService settings,
        GlobalHotkeyRouter router,
        WpfScreenService screens,
        Dispatcher dispatcher)
    {
        _settings = settings;
        _screens = screens;
        _dispatcher = dispatcher;
        _hotkeyRegistration = router.Register(300, OnKeyDown, OnKeyUp);
    }

    public event Action<string, string>? NotificationRequested;

    public bool IsCapturing => _isCapturing;

    private bool OnKeyDown(int vkCode)
    {
        if (_disposed) return false;

        if (vkCode == GlobalHotkeyService.VK_OEM_3)
        {
            CaptureSettings capture = _settings.Current.Capture;
            if (!capture.Enabled) return false;
            if (!MatchesCaptureShortcut(capture)) return false;
            if (_isCapturing) return true;
            _dispatcher.BeginInvoke(StartCapture);
            return true;
        }

        if (vkCode == GlobalHotkeyService.VK_ESCAPE)
        {
            if (_preview?.IsColorPicking == true)
            {
                _dispatcher.BeginInvoke(EndColorPick);
                return true;
            }

            if (_isCapturing)
            {
                _dispatcher.BeginInvoke(CancelCapture);
                return true;
            }
        }

        return false;
    }

    private bool OnKeyUp(int vkCode) => false;

    private bool MatchesCaptureShortcut(CaptureSettings capture)
    {
        bool ctrl = IsDown(VkControl) || IsDown(VkLControl) || IsDown(VkRControl);
        bool alt = IsDown(VkMenu) || IsDown(VkLMenu) || IsDown(VkRMenu);
        bool shift = IsDown(VkShift);
        bool win = IsDown(VkLWin) || IsDown(VkRWin);

        if (capture.AllowBareBacktick)
            return !ctrl && !alt && !shift && !win;

        return ShortcutDefinition.NormalizeCaptureModifier(capture.Modifier) switch
        {
            ShortcutDefinition.CaptureCtrl =>
                IsCtrlForScreenshot() && !alt && !shift && !win,
            ShortcutDefinition.CaptureCtrlShift =>
                IsCtrlForScreenshot() && shift && !alt && !win,
            ShortcutDefinition.CaptureWin =>
                win && !ctrl && !alt && !shift,
            _ =>
                IsAltForScreenshot() && !ctrl && !shift && !win,
        };
    }

    private bool IsAltForScreenshot()
    {
        string voice = ShortcutDefinition.NormalizeVoiceShortcut(_settings.Current.Voice.Shortcut);
        return voice == ShortcutDefinition.LeftAlt ? IsDown(VkRMenu) : IsDown(VkLMenu);
    }

    private bool IsCtrlForScreenshot()
    {
        string voice = ShortcutDefinition.NormalizeVoiceShortcut(_settings.Current.Voice.Shortcut);
        return voice switch
        {
            ShortcutDefinition.LeftCtrl => IsDown(VkRControl),
            ShortcutDefinition.RightCtrl => IsDown(VkLControl),
            _ => IsDown(VkControl) || IsDown(VkLControl) || IsDown(VkRControl),
        };
    }

    public async void StartCapture()
    {
        if (_disposed || _isCapturing) return;
        _isCapturing = true;

        try
        {
            _virtualBounds = _screens.VirtualBounds;
            _visibility = WindowVisibilityScope.HideOwnedWindows();
            if (_visibility.HiddenCount > 0)
                await Task.Delay(150);

            if (!_isCapturing) return;

            _fullImage = ScreenCaptureService.CaptureVirtualDesktop();
            if (_fullImage is null)
            {
                Notify("截屏失败", "无法获取屏幕图像。");
                FinishCapture();
                return;
            }

            double scale = _screens.GetScaleForPoint(
                _virtualBounds.X + _virtualBounds.Width / 2,
                _virtualBounds.Y + _virtualBounds.Height / 2);

            _selector = new RegionSelectorWindow(_fullImage, _virtualBounds, scale);
            _selector.SelectionConfirmed += OnSelectionConfirmed;
            _selector.SelectionCancelled += OnSelectionCancelled;
            _selector.Show();
            _selector.Activate();
        }
        catch (Exception ex)
        {
            Notify("截屏失败", ex.Message);
            FinishCapture();
        }
    }

    private void OnSelectionConfirmed(object? sender, PixelRect localRect)
    {
        if (_disposed || _fullImage is null || !_isCapturing) return;

        try
        {
            BitmapSource? region = ScreenCaptureService.Crop(_fullImage, localRect);
            if (region is null)
            {
                Notify("截屏失败", "无法裁剪选区。");
                FinishCapture();
                return;
            }

            _regionImage = region;
            _selectionScreenRect = new PixelRect(
                _virtualBounds.X + localRect.X,
                _virtualBounds.Y + localRect.Y,
                localRect.Width,
                localRect.Height);

            CloseSelector();
            OpenPreviewAndToolbar();
        }
        catch (Exception ex)
        {
            Notify("截屏失败", ex.Message);
            FinishCapture();
        }
    }

    private void OnSelectionCancelled(object? sender, EventArgs e) => FinishCapture();

    private void OpenPreviewAndToolbar()
    {
        if (_regionImage is null) return;

        double scale = _screens.GetScaleForPoint(
            _selectionScreenRect.X + _selectionScreenRect.Width / 2,
            _selectionScreenRect.Y + _selectionScreenRect.Height / 2);

        _preview = new ScreenshotPreviewWindow();
        _preview.DragPositionChanged += OnPreviewPositionChanged;
        _preview.Closed += OnPreviewClosed;
        _preview.ColorCopied += OnColorCopied;
        _preview.ColorPickEnded += OnColorPickEnded;
        _preview.ShowAt(_regionImage, _selectionScreenRect, scale);

        _toolbar = new ScreenshotToolbarWindow(_screens);
        _toolbar.OcrRequested += OnOcrRequested;
        _toolbar.ColorPickRequested += OnColorPickRequested;
        _toolbar.CopyRequested += OnCopyRequested;
        _toolbar.SaveRequested += OnSaveRequested;
        _toolbar.DoneRequested += OnDoneRequested;
        _toolbar.ShowAt(_selectionScreenRect);
        _toolbar.SetIdle();
        _toolbar.SetOcrEnabled(true);
        _toolbar.SetColorPickEnabled(true);
    }

    private void OnPreviewPositionChanged(object? sender, PixelRect rect)
    {
        _selectionScreenRect = rect;
        _toolbar?.MoveNear(rect);
        _ocrWindow?.MoveTo(rect);
    }

    private void OnPreviewClosed(object? sender, EventArgs e)
    {
        if (_closing) return;
        FinishCapture();
    }

    private void OnCopyRequested(object? sender, EventArgs e)
    {
        if (_regionImage is null) return;
        bool copied = ScreenCaptureService.CopyToClipboard(_regionImage);
        FinishCapture();
        Notify(copied ? "已复制" : "复制失败",
            copied ? "选区图片已复制到剪贴板。" : "无法写入剪贴板。");
    }

    private void OnSaveRequested(object? sender, EventArgs e)
    {
        if (_regionImage is null) return;

        try
        {
            Directory.CreateDirectory(AppPaths.ScreenshotsDirectory);
            var dialog = new SaveFileDialog
            {
                Title = "保存截图",
                Filter = "PNG 图片|*.png",
                DefaultExt = ".png",
                AddExtension = true,
                FileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                InitialDirectory = AppPaths.ScreenshotsDirectory,
            };

            bool? result = _toolbar is null ? dialog.ShowDialog() : dialog.ShowDialog(_toolbar);
            if (result != true) return;

            ScreenCaptureService.SavePng(_regionImage, dialog.FileName);
            _toolbar?.ShowToast($"已保存: {Path.GetFileName(dialog.FileName)}");
            Notify("已保存", dialog.FileName);
        }
        catch (Exception ex)
        {
            _toolbar?.ShowToast("保存失败");
            Notify("保存失败", ex.Message);
        }
    }

    private void OnDoneRequested(object? sender, EventArgs e) => FinishCapture();

    private async void OnOcrRequested(object? sender, EventArgs e)
    {
        if (_regionImage is null || _ocrBusy || _ocrWindow is not null) return;

        if (!CoreServices.Components.IsInstalled(ComponentCatalog.Ocr))
        {
            Notify("OCR 引擎组件未安装", "请在 设置 → 服务与模型 中下载 OCR 引擎组件（约 12 MB，含 onnxruntime）。");
            return;
        }

        _ocrBusy = true;
        BitmapSource image = _regionImage;
        ShowOcrResultDialog();

        try
        {
            _ocrEngine ??= new OcrEngine();
            string? text = await _ocrEngine.RecognizeAsync(
                image,
                message => _dispatcher.BeginInvoke(() => _ocrWindow?.SetBusy(message)));

            if (string.IsNullOrWhiteSpace(text))
            {
                string failure = _ocrEngine?.LastError ?? "未识别到文字";
                _ocrWindow?.ShowFailure(failure);
                Notify("OCR", failure);
            }
            else
            {
                _ocrWindow?.ShowResult(text);
                Notify("OCR 识别完成", "已弹出识别结果窗口。");
            }
        }
        catch (Exception ex)
        {
            string message = ex is FileNotFoundException or DllNotFoundException or TypeLoadException
                ? "OCR 引擎组件未安装或加载失败，请在 设置 → 服务与模型 中重新安装。"
                : ex.Message;
            _ocrWindow?.ShowFailure($"识别失败：{message}");
            Notify("OCR 识别失败", message);
        }
        finally
        {
            _ocrBusy = false;
        }
    }

    private void OnColorPickRequested(object? sender, EventArgs e)
    {
        if (_preview is null || _regionImage is null || _preview.IsColorPicking || _ocrBusy) return;

        // 取色直接在截图预览图上进行，预览保持可见；工具条暂时收起避免干扰。
        if (_toolbar is not null)
            _toolbar.Hide();

        _lastPickedColor = null;
        _preview.StartColorPick();
    }

    private void OnColorCopied(object? sender, string value) => _lastPickedColor = value;

    private void OnColorPickEnded(object? sender, EventArgs e)
    {
        if (_closing) return;
        RestoreAfterColorPick();
    }

    private void EndColorPick() => _preview?.StopColorPick();

    private void RestoreAfterColorPick()
    {
        if (!_isCapturing) return;

        if (_preview is { IsVisible: false })
            _preview.Show();
        if (_toolbar is not null)
        {
            if (!_toolbar.IsVisible)
                _toolbar.Show();
            _toolbar.MoveNear(_selectionScreenRect);
        }

        if (!string.IsNullOrEmpty(_lastPickedColor))
        {
            _toolbar?.ShowToast($"已复制 {_lastPickedColor}");
            Notify("已复制颜色", _lastPickedColor);
            _lastPickedColor = null;
        }
    }

    private void ShowOcrResultDialog()
    {
        if (_ocrWindow is not null) return;

        // 保留截图预览，让识别结果浮在图片上方；预览与结果窗同为 Topmost，
        // 由 OcrResultWindow 显示后自行抬到最前（见 ShowBusy）。
        CloseToolbar();

        var window = new OcrResultWindow(_screens);
        _ocrWindow = window;
        window.Closed += OnOcrWindowClosed;
        window.ShowBusy("正在准备 OCR 模型...", _selectionScreenRect);
    }

    private void OnOcrWindowClosed(object? sender, EventArgs e)
    {
        if (sender is OcrResultWindow window)
            window.Closed -= OnOcrWindowClosed;
        _ocrWindow = null;
        if (!_closing) FinishCapture();
    }

    public void CancelCapture()
    {
        if (!_isCapturing) return;
        FinishCapture();
    }

    private void FinishCapture()
    {
        if (_closing) return;
        _closing = true;
        try
        {
            CloseSelector();
            CloseOcrWindow();
            ClosePreview();
            CloseToolbar();
            _regionImage = null;
            _fullImage = null;
            _selectionScreenRect = default;
            _isCapturing = false;
            _visibility?.Restore();
            _visibility = null;
        }
        finally
        {
            _closing = false;
        }
    }

    private void CloseSelector()
    {
        if (_selector is null) return;
        RegionSelectorWindow selector = _selector;
        _selector = null;
        selector.SelectionConfirmed -= OnSelectionConfirmed;
        selector.SelectionCancelled -= OnSelectionCancelled;
        try { selector.Close(); } catch { }
    }

    private void ClosePreview()
    {
        if (_preview is null) return;
        ScreenshotPreviewWindow preview = _preview;
        _preview = null;
        preview.DragPositionChanged -= OnPreviewPositionChanged;
        preview.Closed -= OnPreviewClosed;
        preview.ColorCopied -= OnColorCopied;
        preview.ColorPickEnded -= OnColorPickEnded;
        try { preview.Close(); } catch { }
    }

    private void CloseToolbar()
    {
        if (_toolbar is null) return;
        ScreenshotToolbarWindow toolbar = _toolbar;
        _toolbar = null;
        toolbar.OcrRequested -= OnOcrRequested;
        toolbar.ColorPickRequested -= OnColorPickRequested;
        toolbar.CopyRequested -= OnCopyRequested;
        toolbar.SaveRequested -= OnSaveRequested;
        toolbar.DoneRequested -= OnDoneRequested;
        try { toolbar.Close(); } catch { }
    }

    private void CloseOcrWindow()
    {
        if (_ocrWindow is null) return;
        OcrResultWindow window = _ocrWindow;
        _ocrWindow = null;
        window.Closed -= OnOcrWindowClosed;
        try { window.Close(); } catch { }
    }

    private void Notify(string title, string message)
    {
        if (_settings.Current.Ui.ShowNotifications)
            NotificationRequested?.Invoke(title, message);
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose()
    {
        if (_disposed) return;
        _hotkeyRegistration.Dispose();
        FinishCapture();
        if (!_ocrBusy)
        {
            _ocrEngine?.Dispose();
            _ocrEngine = null;
        }
        _disposed = true;
    }

    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
