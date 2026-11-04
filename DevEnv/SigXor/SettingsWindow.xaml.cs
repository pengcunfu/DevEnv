using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DevEnv.Core.SigXor.Toolkit;
using DevEnv.Host.SigXor.Services;
using DevEnv.Host.SigXor.Toolkit.Models;
using DevEnv.Host.SigXor.Toolkit.Ocr;
using DevEnv.Host.SigXor.Toolkit.Voice;

namespace DevEnv.Host.SigXor;

public enum SettingsTab
{
    General = 0,
    Voice = 1,
    Service = 2,
}

/// <summary>设置窗口：常规、语音与截屏、服务与模型。</summary>
public partial class SettingsWindow : Window
{
    private const double MinFontSize = 8;
    private const double MaxFontSize = 72;
    private const double MinHexFontSize = 8;
    private const double MaxHexFontSize = 32;
    private const int MinBytesPerRow = 8;
    private const int MaxBytesPerRow = 32;
    private const double MinHoldThreshold = 0.1;
    private const double MaxHoldThreshold = 5;
    private const double MinTypingDelay = 0;
    private const double MaxTypingDelay = 2;

    private readonly SettingsService _settings;
    private readonly VoiceInputService? _voiceService;
    private bool _modelDownloading;
    private bool _ocrModelDownloading;
    private bool _modelMigrating;
    private bool _allowClose;

    public SettingsWindow(
        SettingsService settings,
        VoiceInputService? voiceService = null,
        SettingsTab initialTab = SettingsTab.General)
    {
        InitializeComponent();
        _settings = settings;
        _voiceService = voiceService;

        var monoFonts = Fonts.SystemFontFamilies
            .Where(f => IsMonospaced(f))
            .OrderBy(f => f.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FontFamilyCombo.ItemsSource = monoFonts;

        LoadGeneral(_settings.Current);
        LoadToolkit(_settings.Current);
        SelectTab(initialTab);

        VoiceShortcutCombo.SelectionChanged += (_, _) => UpdateShortcutHint();
        CaptureShortcutCombo.SelectionChanged += (_, _) => UpdateShortcutHint();
        AllowBareBacktickCheck.Checked += (_, _) => UpdateShortcutHint();
        AllowBareBacktickCheck.Unchecked += (_, _) => UpdateShortcutHint();

        if (_voiceService is not null)
            _voiceService.StatusChanged += OnVoiceStatusChanged;
        Closed += OnWindowClosed;
        Closing += OnClosing;
        RefreshServiceUi();
    }

    public void SelectTab(SettingsTab tab) => MainTabs.SelectedIndex = (int)tab;

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !_settings.Current.Ui.MinimizeToTray) return;
        e.Cancel = true;
        Hide();
    }

    private static bool IsMonospaced(FontFamily family)
    {
        var typefaces = family.GetTypefaces();
        if (typefaces.Count == 0) return false;
        if (!typefaces.First().TryGetGlyphTypeface(out var gt)) return false;
        ushort iGlyph = gt.CharacterToGlyphMap.TryGetValue('i', out var ig) ? ig : (ushort)0;
        ushort wGlyph = gt.CharacterToGlyphMap.TryGetValue('W', out var wg) ? wg : (ushort)0;
        return gt.AdvanceWidths[iGlyph] == gt.AdvanceWidths[wGlyph];
    }

    private void LoadGeneral(AppSettings s)
    {
        AutoStartCheck.IsChecked = s.AutoStartEnabled;
        BackupCheck.IsChecked = s.BackupEnabled;
        CheckUpdatesCheck.IsChecked = s.CheckUpdatesOnStartup;
        FontFamilyCombo.Text = s.TextFontFamily;
        TextFontSizeBox.Text = s.TextFontSize.ToString("0.##", CultureInfo.InvariantCulture);
        HexFontSizeBox.Text = s.HexFontSize.ToString("0.##", CultureInfo.InvariantCulture);
        BytesPerRowBox.Text = s.BytesPerRow.ToString(CultureInfo.InvariantCulture);
    }

    private void LoadToolkit(AppSettings s)
    {
        VoiceSettings v = s.Voice;
        CaptureSettings c = s.Capture;
        UiSettings u = s.Ui;

        SelectComboByTag(VoiceEngineCombo, string.IsNullOrWhiteSpace(v.Engine) ? "sensevoice" : v.Engine);
        SelectComboByTag(VoiceShortcutCombo, ShortcutDefinition.NormalizeVoiceShortcut(v.Shortcut));
        SelectComboByTag(VoiceLanguageCombo, v.Language);
        UseClipboardCheck.IsChecked = v.UseClipboard;
        VoiceAutoStartCheck.IsChecked = v.AutoStartService;
        ShowNotificationsCheck.IsChecked = u.ShowNotifications;
        DebugModeCheck.IsChecked = v.DebugMode;
        SaveAudioCheck.IsChecked = v.SaveAudioFiles;
        HoldThresholdBox.Text = v.HoldThresholdSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        TypingDelayBox.Text = v.TypingDelay.ToString("0.##", CultureInfo.InvariantCulture);

        CaptureEnabledCheck.IsChecked = c.Enabled;
        SelectComboByTag(CaptureShortcutCombo, ShortcutDefinition.NormalizeCaptureModifier(c.Modifier));
        AllowBareBacktickCheck.IsChecked = c.AllowBareBacktick;
        MinimizeToTrayCheck.IsChecked = u.MinimizeToTray;
        SilentStartCheck.IsChecked = u.SilentStart;
        UpdateShortcutHint();
    }

    private void UpdateShortcutHint()
    {
        if (ShortcutHintText is null) return;

        string voice = ShortcutDefinition.FormatVoiceShortcut(GetTag(VoiceShortcutCombo));
        string capture = ShortcutDefinition.FormatCaptureModifier(
            GetTag(CaptureShortcutCombo),
            GetTag(VoiceShortcutCombo),
            AllowBareBacktickCheck.IsChecked == true);

        ShortcutHintText.Text = $"语音：{voice}；截屏：{capture}。截屏会优先使用未被语音占用的另一侧 Alt/Ctrl。";
        ServiceUsageHintText.Text =
            $"使用说明：{voice} 短按开始/再按结束，或按住说话、松手结束；截屏 {capture} 拖动选择区域。";
    }

    private void RefreshServiceUi()
    {
        bool installed = SpeechModelManager.IsInstalled;
        bool legacy = SpeechModelManager.IsUsingLegacy;
        long size = SpeechModelManager.GetSizeOnDisk();

        if (!_modelDownloading)
        {
            SpeechModelStatusText.Text = installed
                ? $"{SpeechModelManager.StatusText} · {SpeechModelManager.FormatSize(size)}"
                : "未下载（约 230 MB）";
        }

        DownloadSpeechModelButton.IsEnabled = !installed && !_modelDownloading;
        DeleteSpeechModelButton.IsEnabled = installed && !legacy && !_modelDownloading;
        DeleteSpeechModelButton.ToolTip = legacy ? "当前复用的是旧版 SigXor 目录中的模型，无法在此删除。" : null;
        OpenSpeechModelFolderButton.IsEnabled = true;

        bool ocrReady = OcrModelManager.IsReady;
        bool ocrLegacy = OcrModelManager.IsUsingLegacy;
        long ocrSize = OcrModelManager.GetSizeOnDisk();
        if (!_ocrModelDownloading)
        {
            OcrModelStatusText.Text = ocrReady
                ? $"{OcrModelManager.StatusText} · {OcrModelManager.FormatSize(ocrSize)}"
                : "未下载（约 30 MB / 回退 v5 约 16 MB）";
        }
        DownloadOcrModelButton.IsEnabled = !ocrReady && !_ocrModelDownloading;
        DeleteOcrModelButton.IsEnabled = ocrReady && !ocrLegacy && !_ocrModelDownloading;
        DeleteOcrModelButton.ToolTip = ocrLegacy ? "当前复用的是旧版 SigXor 目录中的模型，无法在此删除。" : null;
        OpenOcrModelFolderButton.IsEnabled = true;

        bool running = _voiceService?.IsRunning == true;
        bool processing = _voiceService?.IsProcessing == true;
        StartVoiceServiceButton.IsEnabled = !running && !processing && !_modelDownloading && installed;
        StopVoiceServiceButton.IsEnabled = running || processing;
        if (!_modelMigrating)
        {
            if (_voiceService is not null)
                ServiceStatusText.Text = _voiceService.Status.Message;
            else
                ServiceStatusText.Text = installed ? "语音服务未接入" : "模型未下载";
        }

        bool canMigrate = SpeechModelManager.IsUsingLegacy || OcrModelManager.IsUsingLegacy;
        MigrateLegacyModelsButton.IsEnabled = canMigrate
            && !_modelDownloading && !_ocrModelDownloading && !_modelMigrating;
    }

    private void OnVoiceStatusChanged(object? sender, VoiceServiceStatus status)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshServiceUi);
            return;
        }
        RefreshServiceUi();
    }

    private async void OnStartVoiceServiceClick(object sender, RoutedEventArgs e)
    {
        if (_voiceService is null) return;
        StartVoiceServiceButton.IsEnabled = false;
        await _voiceService.StartServiceAsync();
        RefreshServiceUi();
    }

    private void OnStopVoiceServiceClick(object sender, RoutedEventArgs e)
    {
        _voiceService?.StopService();
        RefreshServiceUi();
    }

    private async void OnDownloadSpeechModelClick(object sender, RoutedEventArgs e)
    {
        _modelDownloading = true;
        SpeechDownloadProgressBar.Visibility = Visibility.Visible;
        SpeechDownloadProgressBar.IsIndeterminate = true;
        SpeechDownloadProgressBar.Value = 0;
        SpeechModelStatusText.Text = "准备下载...";
        RefreshServiceUi();

        var progress = new Progress<ModelDownloadProgress>(p =>
        {
            if (p.Percent >= 0)
            {
                SpeechDownloadProgressBar.IsIndeterminate = false;
                SpeechDownloadProgressBar.Value = p.Percent;
            }
            else
            {
                SpeechDownloadProgressBar.IsIndeterminate = true;
            }
            SpeechModelStatusText.Text = p.Message;
        });

        bool ok;
        try
        {
            ok = await SpeechModelManager.DownloadAsync(progress);
        }
        catch (OperationCanceledException)
        {
            ok = false;
            SpeechModelStatusText.Text = "已取消下载";
        }
        catch (Exception ex)
        {
            ok = false;
            SpeechModelStatusText.Text = $"下载失败：{ex.Message}";
        }
        finally
        {
            _modelDownloading = false;
            SpeechDownloadProgressBar.Visibility = Visibility.Collapsed;
            SpeechDownloadProgressBar.IsIndeterminate = false;
        }

        if (!ok)
        {
            MessageBox.Show("模型下载失败，请检查网络后重试。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshServiceUi();
    }

    private void OnDeleteSpeechModelClick(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            "删除已下载的 SenseVoice 模型？下次使用需要重新下载（约 230 MB）。",
            "SigXor",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        _voiceService?.StopService();
        if (!SpeechModelManager.Delete(out string? error))
        {
            MessageBox.Show(error ?? "删除模型失败。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshServiceUi();
    }

    private void OnOpenSpeechModelFolderClick(object sender, RoutedEventArgs e)
    {
        try { SpeechModelManager.OpenFolder(); }
        catch (Exception ex)
        {
            MessageBox.Show($"打开模型目录失败：{ex.Message}", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnDownloadOcrModelClick(object sender, RoutedEventArgs e)
    {
        _ocrModelDownloading = true;
        OcrDownloadProgressBar.Visibility = Visibility.Visible;
        OcrDownloadProgressBar.IsIndeterminate = true;
        OcrModelStatusText.Text = "准备下载...";
        RefreshServiceUi();

        var progress = new Progress<string>(message => OcrModelStatusText.Text = message);
        Action<string> report = ((IProgress<string>)progress).Report;
        bool ok;
        try
        {
            ok = await OcrModelManager.EnsureReadyAsync(report);
        }
        catch (OperationCanceledException)
        {
            ok = false;
            OcrModelStatusText.Text = "已取消下载";
        }
        catch (Exception ex)
        {
            ok = false;
            OcrModelStatusText.Text = $"下载失败：{ex.Message}";
        }
        finally
        {
            _ocrModelDownloading = false;
            OcrDownloadProgressBar.Visibility = Visibility.Collapsed;
        }

        if (!ok)
        {
            MessageBox.Show("OCR 模型下载失败，请检查网络后重试。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshServiceUi();
    }

    private void OnDeleteOcrModelClick(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            "删除已下载的 OCR 模型？下次使用需要重新下载。",
            "SigXor",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        if (!OcrModelManager.Delete(out string? error))
        {
            MessageBox.Show(error ?? "删除 OCR 模型失败。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshServiceUi();
    }

    private void OnOpenOcrModelFolderClick(object sender, RoutedEventArgs e)
    {
        try { OcrModelManager.OpenFolder(); }
        catch (Exception ex)
        {
            MessageBox.Show($"打开 OCR 模型目录失败：{ex.Message}", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnMigrateLegacyModelsClick(object sender, RoutedEventArgs e)
    {
        _modelMigrating = true;
        RefreshServiceUi();
        ServiceStatusText.Text = "正在从旧版 SigXor 迁移模型...";

        var progress = new Progress<string>(message => ServiceStatusText.Text = message);
        Action<string> report = ((IProgress<string>)progress).Report;
        string result;
        try
        {
            bool speech = await SpeechModelManager.MigrateFromLegacyAsync(report);
            bool ocr = await OcrModelManager.MigrateFromLegacyAsync(report);
            result = speech || ocr ? "模型迁移完成" : "未找到可迁移的 SigXor 模型";
        }
        catch (Exception ex)
        {
            result = $"模型迁移失败：{ex.Message}";
        }
        finally
        {
            _modelMigrating = false;
            RefreshServiceUi();
        }
        ServiceStatusText.Text = result;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_voiceService is not null)
            _voiceService.StatusChanged -= OnVoiceStatusChanged;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        string fontFamilyText = FontFamilyCombo.Text.Trim();
        if (string.IsNullOrWhiteSpace(fontFamilyText))
        {
            MessageBox.Show("请输入或选择字体名称。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(TextFontSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double textFontSize)
            || textFontSize < MinFontSize || textFontSize > MaxFontSize)
        {
            MessageBox.Show($"文本字号需在 {MinFontSize:0}–{MaxFontSize:0} 之间。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(HexFontSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double fontSize)
            || fontSize < MinHexFontSize || fontSize > MaxHexFontSize)
        {
            MessageBox.Show($"十六进制字号需在 {MinHexFontSize:0}–{MaxHexFontSize:0} 之间。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(BytesPerRowBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bytesPerRow)
            || bytesPerRow < MinBytesPerRow || bytesPerRow > MaxBytesPerRow)
        {
            MessageBox.Show($"每行字节数需在 {MinBytesPerRow}–{MaxBytesPerRow} 之间。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(HoldThresholdBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double holdThreshold)
            || holdThreshold < MinHoldThreshold || holdThreshold > MaxHoldThreshold)
        {
            MessageBox.Show($"长按阈值需在 {MinHoldThreshold:0.#}–{MaxHoldThreshold:0.#} 秒之间。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(TypingDelayBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typingDelay)
            || typingDelay < MinTypingDelay || typingDelay > MaxTypingDelay)
        {
            MessageBox.Show($"输入延迟需在 {MinTypingDelay:0.#}–{MaxTypingDelay:0.#} 秒之间。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool autoStart = AutoStartCheck.IsChecked == true;
        bool silentStart = SilentStartCheck.IsChecked == true;
        AutoStartService.Apply(autoStart, silentStart);
        if (AutoStartService.IsEnabled() != autoStart)
        {
            MessageBox.Show("开机自启动设置失败，请稍后重试。", "SigXor",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var s = _settings.Current;
        s.AutoStartEnabled = autoStart;
        s.BackupEnabled = BackupCheck.IsChecked == true;
        s.CheckUpdatesOnStartup = CheckUpdatesCheck.IsChecked == true;
        s.TextFontFamily = fontFamilyText;
        s.TextFontSize = textFontSize;
        s.HexFontSize = fontSize;
        s.BytesPerRow = bytesPerRow;

        s.Voice.Engine = GetTag(VoiceEngineCombo) ?? "sensevoice";
        s.Voice.Shortcut = ShortcutDefinition.NormalizeVoiceShortcut(GetTag(VoiceShortcutCombo));
        s.Voice.Language = GetTag(VoiceLanguageCombo) ?? "auto";
        s.Voice.UseClipboard = UseClipboardCheck.IsChecked == true;
        s.Voice.AutoStartService = VoiceAutoStartCheck.IsChecked == true;
        s.Voice.DebugMode = DebugModeCheck.IsChecked == true;
        s.Voice.SaveAudioFiles = SaveAudioCheck.IsChecked == true;
        s.Voice.HoldThresholdSeconds = holdThreshold;
        s.Voice.TypingDelay = typingDelay;

        s.Capture.Enabled = CaptureEnabledCheck.IsChecked == true;
        s.Capture.Modifier = ShortcutDefinition.NormalizeCaptureModifier(GetTag(CaptureShortcutCombo));
        s.Capture.AllowBareBacktick = AllowBareBacktickCheck.IsChecked == true;

        s.Ui.ShowNotifications = ShowNotificationsCheck.IsChecked == true;
        s.Ui.MinimizeToTray = MinimizeToTrayCheck.IsChecked == true;
        s.Ui.SilentStart = silentStart;

        _settings.Save();
        Close();
    }

    private void OnResetToolkitClick(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            "恢复语音与截屏的默认设置？常规设置不会改变。",
            "SigXor",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        var s = _settings.Current;
        s.Voice = new VoiceSettings();
        s.Capture = new CaptureSettings();
        s.Ui = new UiSettings();
        _settings.Save();
        LoadToolkit(s);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private static void SelectComboByTag(ComboBox comboBox, string? tagValue)
    {
        if (string.IsNullOrEmpty(tagValue)) return;
        foreach (object item in comboBox.Items)
        {
            if (item is ComboBoxItem { Tag: not null } comboItem
                && string.Equals(comboItem.Tag.ToString(), tagValue, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = comboItem;
                return;
            }
        }
        if (comboBox.Items.Count > 0) comboBox.SelectedIndex = 0;
    }

    private static string? GetTag(ComboBox comboBox) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
}
