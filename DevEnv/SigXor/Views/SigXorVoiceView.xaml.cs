using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DevEnv.Core.SigXor.Toolkit;

namespace DevEnv.Host.SigXor.Views;

/// <summary>语音输入与区域截屏的设置项。</summary>
public partial class SigXorVoiceView : UserControl
{
    private readonly Services.SettingsService _settings;

    public SigXorVoiceView(Services.SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        VoiceShortcutCombo.SelectionChanged += (_, _) => UpdateShortcutHint();
        CaptureShortcutCombo.SelectionChanged += (_, _) => UpdateShortcutHint();
        AllowBareBacktickCheck.Checked += (_, _) => UpdateShortcutHint();
        AllowBareBacktickCheck.Unchecked += (_, _) => UpdateShortcutHint();

        Load();
    }

    private void Load()
    {
        var s = _settings.Current;
        VoiceSettings v = s.Voice;
        CaptureSettings c = s.Capture;

        SelectComboByTag(VoiceEngineCombo, string.IsNullOrWhiteSpace(v.Engine) ? "sensevoice" : v.Engine);
        SelectComboByTag(VoiceShortcutCombo, ShortcutDefinition.NormalizeVoiceShortcut(v.Shortcut));
        SelectComboByTag(VoiceLanguageCombo, v.Language);
        UseClipboardCheck.IsChecked = v.UseClipboard;
        VoiceAutoStartCheck.IsChecked = v.AutoStartService;
        ShowNotificationsCheck.IsChecked = s.Ui.ShowNotifications;
        DebugModeCheck.IsChecked = v.DebugMode;
        SaveAudioCheck.IsChecked = v.SaveAudioFiles;
        HoldThresholdBox.Text = v.HoldThresholdSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        TypingDelayBox.Text = v.TypingDelay.ToString("0.##", CultureInfo.InvariantCulture);

        CaptureEnabledCheck.IsChecked = c.Enabled;
        SelectComboByTag(CaptureShortcutCombo, ShortcutDefinition.NormalizeCaptureModifier(c.Modifier));
        AllowBareBacktickCheck.IsChecked = c.AllowBareBacktick;
        UpdateShortcutHint();
    }

    /// <summary>当前生效的快捷键说明（供「服务与模型」页复用）。</summary>
    public string ShortcutHint
    {
        get
        {
            string voice = ShortcutDefinition.FormatVoiceShortcut(GetTag(VoiceShortcutCombo));
            string capture = ShortcutDefinition.FormatCaptureModifier(
                GetTag(CaptureShortcutCombo),
                GetTag(VoiceShortcutCombo),
                AllowBareBacktickCheck.IsChecked == true);
            return $"语音：{voice}；截屏：{capture}。截屏会优先使用未被语音占用的另一侧 Alt/Ctrl。";
        }
    }

    private void UpdateShortcutHint()
    {
        if (ShortcutHintText is null) return;
        ShortcutHintText.Text = ShortcutHint;
    }

    /// <summary>把控件上的值写回设置；校验失败返回 false。</summary>
    public bool Save(out string? error)
    {
        error = null;

        if (!double.TryParse(HoldThresholdBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double holdThreshold)
            || holdThreshold < 0.1 || holdThreshold > 5)
        {
            error = "长按阈值需在 0.1–5 秒之间。";
            return false;
        }

        if (!double.TryParse(TypingDelayBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typingDelay)
            || typingDelay < 0 || typingDelay > 2)
        {
            error = "输入延迟需在 0–2 秒之间。";
            return false;
        }

        var s = _settings.Current;
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
        _settings.Save();
        return true;
    }

    /// <summary>恢复语音与截屏的默认设置。</summary>
    public void ResetToDefaults()
    {
        var s = _settings.Current;
        s.Voice = new VoiceSettings();
        s.Capture = new CaptureSettings();
        s.Ui = new UiSettings();
        _settings.Save();
        Load();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            "恢复语音与截屏的默认设置？常规设置不会改变。",
            "燧岚开发盒子",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        ResetToDefaults();
        Message.Text = "已恢复默认";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!Save(out string? error))
        {
            MessageBox.Show(error ?? "保存失败。", "燧岚开发盒子",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Message.Text = "已保存";
    }

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
