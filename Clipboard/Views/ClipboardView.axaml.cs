using System.Drawing.Imaging;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ClipboardApp.Models;
using ClipboardApp.Services;
using ClipboardApp.UI;
using DrawingImage = System.Drawing.Image;
using FormsClipboard = System.Windows.Forms.Clipboard;

namespace ClipboardApp.Views;

public partial class ClipboardView : Avalonia.Controls.UserControl
{
    private readonly ConfigService _config = new();
    private readonly HistoryService _history = new();
    private readonly DispatcherTimer _clipboardTimer;

    private HotkeyService? _hotkey;
    private bool _monitoringEnabled = true;
    private bool _suppressClipboard;
    private bool _started;
    private string? _lastClipboardSignature;
    private DateTime _lastImageCapture = DateTime.MinValue;
    private ClipboardEntry? _currentImageEntry;
    private ClipboardEntry? _currentTextEntry;

    /// <summary>请求宿主切换到本 Tab 并激活主窗。</summary>
    public Action? RequestActivateHost { get; set; }

    public ClipboardView()
    {
        InitializeComponent();
        AppPaths.MigrateLegacyData();

        HistoryList.ItemsSource = _history.Entries;

        _clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _clipboardTimer.Tick += (_, _) => PollClipboard();
        // 构造后即监听（不依赖 Tab 是否可见）
        _started = true;
        _clipboardTimer.Start();
        ReloadVisibleView();

        AttachedToVisualTree += (_, _) => EnsureHotkey();
    }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    /// <summary>在主窗可用后注册全局热键。</summary>
    public void EnsureHotkey()
    {
        if (_hotkey is not null || OwnerWindow is not { } owner)
            return;

        _hotkey = new HotkeyService(owner);
        _hotkey.HotkeyPressed += OnHotkeyPressed;
        _hotkey.Register(_config.Config.HotkeyModifiers, _config.Config.HotkeyKey);
    }

    public void DisposeHotkey()
    {
        if (_hotkey is null)
            return;

        _hotkey.HotkeyPressed -= OnHotkeyPressed;
        _hotkey.Dispose();
        _hotkey = null;
    }

    private void OnHotkeyPressed() => RequestActivateHost?.Invoke();

    private void PollClipboard()
    {
        if (!_monitoringEnabled || _suppressClipboard)
            return;

        try
        {
            if (FormsClipboard.ContainsImage())
            {
                using var image = FormsClipboard.GetImage();
                if (image is null)
                    return;

                if (_lastClipboardSignature?.StartsWith("img:", StringComparison.Ordinal) == true
                    && (DateTime.Now - _lastImageCapture).TotalMilliseconds < 800)
                    return;

                CaptureImage(image);
                _lastClipboardSignature = $"img:{image.Width}x{image.Height}";
                return;
            }

            if (FormsClipboard.ContainsText())
            {
                var text = FormsClipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                var signature = "text:" + text;
                if (signature == _lastClipboardSignature)
                    return;

                _lastClipboardSignature = signature;
                CaptureText(text);
            }
        }
        catch
        {
            // 剪贴板被占用时忽略
        }
    }

    private void CaptureText(string text)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var entry = new ClipboardEntry
        {
            Timestamp = timestamp,
            Type = "text",
            Text = text,
        };
        var top = _history.Insert(entry);
        ReloadVisibleView();
        HistoryList.SelectedItem = _history.Entries.FirstOrDefault(e => e.Id == top.Id) ?? top;
    }

    private void CaptureImage(DrawingImage image)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var fileName = $"{timestamp.Replace(':', '-').Replace(' ', '_')}.png";
        var path = Path.Combine(AppPaths.ImagesDir, fileName);

        try
        {
            image.Save(path, ImageFormat.Png);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Save image failed: " + ex.Message);
            return;
        }

        _lastImageCapture = DateTime.Now;
        var entry = new ClipboardEntry
        {
            Timestamp = timestamp,
            Type = "image",
            ImagePath = AppPaths.ImagePathForStorage(path),
        };
        var top = _history.Insert(entry);
        ReloadVisibleView();
        HistoryList.SelectedItem = _history.Entries.FirstOrDefault(e => e.Id == top.Id) ?? top;
    }

    private void MonitorButton_OnClick(object? sender, RoutedEventArgs e) => ApplyMonitoringState();

    private void ApplyMonitoringState()
    {
        _monitoringEnabled = MonitorButton.IsChecked == true;
        if (_monitoringEnabled)
        {
            MonitorButton.Content = UiText.StopListening;
            StatusLabel.Text = UiText.StatusListening;
            StatusLabel.Foreground = Avalonia.Media.Brushes.ForestGreen;
        }
        else
        {
            MonitorButton.Content = UiText.StartListening;
            StatusLabel.Text = UiText.StatusIdle;
            StatusLabel.Foreground = Avalonia.Media.Brushes.Gray;
        }
    }

    private void HistoryList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is not ClipboardEntry entry)
        {
            ClearPreview();
            return;
        }

        if (entry.IsImage)
        {
            var path = AppPaths.ResolveImagePath(entry.ImagePath);
            if (File.Exists(path))
            {
                try
                {
                    PreviewImage.Source = new Bitmap(path);
                }
                catch
                {
                    PreviewImage.Source = null;
                }
            }
            else
            {
                PreviewImage.Source = null;
            }

            PreviewText.IsVisible = false;
            ImageScroll.IsVisible = true;
            _currentImageEntry = entry;
            _currentTextEntry = null;
            PreviewLangLabel.Text = string.Empty;
        }
        else
        {
            var text = entry.Text ?? string.Empty;
            _currentTextEntry = entry;
            _currentImageEntry = null;
            var language = CodeLanguageDetector.Detect(text);
            PreviewText.Text = text;
            PreviewText.IsVisible = true;
            ImageScroll.IsVisible = false;
            PreviewLangLabel.Text = string.IsNullOrEmpty(language) ? string.Empty : $"已识别: {language}";
        }
    }

    private void HistoryList_OnDoubleTapped(object? sender, TappedEventArgs e) => CopySelected();

    private void Copy_OnClick(object? sender, RoutedEventArgs e) => CopySelected();

    private void CopySelected()
    {
        if (HistoryList.SelectedItem is not ClipboardEntry entry)
            return;

        try
        {
            _suppressClipboard = true;
            if (entry.IsImage)
            {
                var path = AppPaths.ResolveImagePath(entry.ImagePath);
                if (!File.Exists(path))
                    return;

                using var img = DrawingImage.FromFile(path);
                FormsClipboard.SetImage(img);
                _lastClipboardSignature = $"img:self:{entry.ImagePath}";
                _lastImageCapture = DateTime.Now;
            }
            else
            {
                FormsClipboard.SetText(entry.Text ?? string.Empty);
                _lastClipboardSignature = "text:" + (entry.Text ?? string.Empty);
            }
        }
        finally
        {
            Dispatcher.UIThread.Post(() => _suppressClipboard = false, DispatcherPriority.Background);
        }
    }

    private async void SaveAsMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not ClipboardEntry entry)
            return;
        await SaveEntryAsync(entry);
    }

    private async void DeleteMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not ClipboardEntry entry)
            return;

        if (!await CbDialog.ConfirmAsync(OwnerWindow, UiText.ConfirmDelete, UiText.ConfirmDeleteTitle))
            return;

        _history.Delete(entry);
        if (_history.Entries.Count == 0)
            ClearPreview();
        else
            ReloadVisibleView();
    }

    private async Task SaveEntryAsync(ClipboardEntry entry)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        if (entry.IsImage)
        {
            var path = AppPaths.ResolveImagePath(entry.ImagePath);
            if (!File.Exists(path))
            {
                await CbDialog.ShowAsync(OwnerWindow, UiText.ImageMissing, UiText.Tip);
                return;
            }

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = UiText.SaveImage,
                SuggestedFileName = Path.GetFileName(path),
                FileTypeChoices =
                [
                    new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"] },
                ],
            });
            if (file is null)
                return;

            try
            {
                await using var src = File.OpenRead(path);
                await using var dst = await file.OpenWriteAsync();
                await src.CopyToAsync(dst);
                await CbDialog.ShowAsync(OwnerWindow, UiText.ImageSaved + "\n" + file.Path.LocalPath, UiText.SaveSuccess);
            }
            catch (Exception ex)
            {
                await CbDialog.ShowAsync(OwnerWindow, UiText.SaveFailed + "\n" + ex.Message, UiText.SaveFailedTitle);
            }
        }
        else
        {
            var stamp = string.IsNullOrWhiteSpace(entry.Timestamp)
                ? DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
                : entry.Timestamp.Replace(':', '-').Replace(' ', '_');

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = UiText.SaveText,
                SuggestedFileName = $"clipboard_{stamp}.txt",
                FileTypeChoices =
                [
                    new FilePickerFileType("Text") { Patterns = ["*.txt"] },
                ],
            });
            if (file is null)
                return;

            try
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(entry.Text ?? string.Empty);
                await CbDialog.ShowAsync(OwnerWindow, UiText.TextSaved + "\n" + file.Path.LocalPath, UiText.SaveSuccess);
            }
            catch (Exception ex)
            {
                await CbDialog.ShowAsync(OwnerWindow, UiText.SaveFailed + "\n" + ex.Message, UiText.SaveFailedTitle);
            }
        }
    }

    private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.Bottom,
        };
        void Add(string header, string mode)
        {
            var item = new MenuItem { Header = header };
            item.Click += async (_, _) => await ClearByRangeAsync(mode);
            menu.Items.Add(item);
        }

        Add(UiText.ClearToday, "today");
        Add(UiText.ClearWeek, "week");
        Add(UiText.ClearMonth, "month");
        menu.Items.Add(new Separator());
        Add(UiText.ClearAll, "all");
        menu.Open(ClearButton);
    }

    private async Task ClearByRangeAsync(string mode)
    {
        var labels = new Dictionary<string, string>
        {
            ["today"] = UiText.RangeToday,
            ["week"] = UiText.RangeWeek,
            ["month"] = UiText.RangeMonth,
            ["all"] = UiText.RangeAll,
        };
        var label = labels.GetValueOrDefault(mode, UiText.RangeSelected);
        var count = _history.CountByRange(mode);
        if (count == 0)
        {
            await CbDialog.ShowAsync(OwnerWindow, string.Format(UiText.NothingToClear, label), UiText.Tip);
            return;
        }

        if (!await CbDialog.ConfirmAsync(OwnerWindow, string.Format(UiText.ConfirmClear, label, count), UiText.ConfirmClearTitle))
            return;

        var removed = _history.ClearByRange(mode);
        if (_history.Entries.Count == 0)
            ClearPreview();
        else if (HistoryList.SelectedItem is null)
            HistoryList.SelectedItem = _history.Entries.FirstOrDefault();

        await CbDialog.ShowAsync(OwnerWindow, string.Format(UiText.Cleared, label, removed), UiText.Tip);
    }

    private async void Export_OnClick(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = UiText.ExportHistory,
            SuggestedFileName = "clipboard_history.txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Text") { Patterns = ["*.txt"] },
            ],
        });
        if (file is null)
            return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            foreach (var entry in _history.Entries)
            {
                if (!entry.IsImage)
                    await writer.WriteLineAsync(entry.Text);
                await writer.WriteLineAsync();
            }

            await CbDialog.ShowAsync(OwnerWindow, UiText.ExportSuccess, UiText.ExportSuccessTitle);
        }
        catch (Exception ex)
        {
            await CbDialog.ShowAsync(OwnerWindow, UiText.ExportFailed + ": " + ex.Message, UiText.ExportFailedTitle);
        }
    }

    private void RangeOrFilter_OnChanged(object? sender, TextChangedEventArgs e) => ReloadVisibleView();

    private void RangeOrFilter_OnChanged(object? sender, SelectionChangedEventArgs e) => ReloadVisibleView();

    private void ReloadVisibleView()
    {
        if (!_started)
            return;

        var query = new HistoryQuery(
            SinceTimestamp: GetRangeSince(),
            Type: GetCategoryType(),
            Search: string.IsNullOrWhiteSpace(SearchBox?.Text) ? null : SearchBox.Text.Trim());
        _history.ReloadView(query);

        if (HistoryList.SelectedItem is not ClipboardEntry selected
            || !_history.Entries.Contains(selected))
        {
            HistoryList.SelectedItem = _history.Entries.FirstOrDefault();
        }
    }

    private string? GetRangeSince()
    {
        var tag = (RangeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return tag switch
        {
            "3days" => HistoryService.DaysAgoTimestamp(3),
            "today" => HistoryService.TodayStartTimestamp(),
            "week" => HistoryService.DaysAgoTimestamp(7),
            "month" => HistoryService.DaysAgoTimestamp(30),
            "all" or null => null,
            _ => null,
        };
    }

    private string? GetCategoryType()
    {
        var tag = (CategoryCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return tag switch
        {
            "text" => "text",
            "image" => "image",
            _ => null,
        };
    }

    private void ClearPreview()
    {
        PreviewText.Text = string.Empty;
        PreviewImage.Source = null;
        PreviewText.IsVisible = false;
        ImageScroll.IsVisible = false;
        _currentImageEntry = null;
        _currentTextEntry = null;
        PreviewLangLabel.Text = string.Empty;
    }

    private async void HotkeySettings_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new HotkeySettingsWindow(_config.Config.HotkeyModifiers, _config.Config.HotkeyKey);
        if (OwnerWindow is { } owner)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();

        if (!dialog.Confirmed)
            return;

        if (dialog.Modifiers.Count == 0)
        {
            await CbDialog.ShowAsync(OwnerWindow, UiText.NeedModifier, UiText.Error);
            return;
        }

        _config.Config.HotkeyModifiers = dialog.Modifiers;
        _config.Config.HotkeyKey = dialog.HotkeyKey;
        _config.Save();

        EnsureHotkey();
        var ok = _hotkey?.Register(dialog.Modifiers, dialog.HotkeyKey) == true;
        var formatted = HotkeyService.FormatHotkey(dialog.Modifiers, dialog.HotkeyKey);
        await CbDialog.ShowAsync(
            OwnerWindow,
            ok ? UiText.HotkeySet + formatted : UiText.HotkeyRegisterFailed + formatted,
            ok ? UiText.Success : UiText.Warning);
    }
}
