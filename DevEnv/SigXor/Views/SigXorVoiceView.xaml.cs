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

        Load();
    }

    private void Load()
    {
        var s = _settings.Current;
        VoiceSettings v = s.Voice;

        SelectComboByTag(VoiceEngineCombo, string.IsNullOrWhiteSpace(v.Engine) ? "sensevoice" : v.Engine);
        SelectComboByTag(VoiceLanguageCombo, v.Language);
        UseClipboardCheck.IsChecked = v.UseClipboard;
        VoiceAutoStartCheck.IsChecked = v.AutoStartService;
        ShowNotificationsCheck.IsChecked = s.Ui.ShowNotifications;
        DebugModeCheck.IsChecked = v.DebugMode;
        SaveAudioCheck.IsChecked = v.SaveAudioFiles;
        HoldThresholdBox.Text = v.HoldThresholdSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        TypingDelayBox.Text = v.TypingDelay.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>当前生效的快捷键说明（供「服务与模型」页复用）。</summary>
    public string ShortcutHint
    {
        get
        {
            var s = _settings.Current;
            string voice = ShortcutDefinition.FormatVoiceShortcut(s.Voice.Shortcut);
            string capture = ShortcutDefinition.FormatCaptureModifier(
                s.Capture.Modifier,
                s.Voice.Shortcut,
                s.Capture.AllowBareBacktick);
            return $"语音：{voice}；截屏：{capture}。截屏会优先使用未被语音占用的另一侧 Alt/Ctrl。";
        }
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
        s.Voice.Language = GetTag(VoiceLanguageCombo) ?? "auto";
        s.Voice.UseClipboard = UseClipboardCheck.IsChecked == true;
        s.Voice.AutoStartService = VoiceAutoStartCheck.IsChecked == true;
        s.Voice.DebugMode = DebugModeCheck.IsChecked == true;
        s.Voice.SaveAudioFiles = SaveAudioCheck.IsChecked == true;
        s.Voice.HoldThresholdSeconds = holdThreshold;
        s.Voice.TypingDelay = typingDelay;

        s.Ui.ShowNotifications = ShowNotificationsCheck.IsChecked == true;
        _settings.Save();
        return true;
    }

    /// <summary>恢复语音相关设置；快捷键由「快捷键」页管理，这里保留当前值。</summary>
    public void ResetToDefaults()
    {
        var s = _settings.Current;
        string voiceShortcut = s.Voice.Shortcut;
        bool captureEnabled = s.Capture.Enabled;
        string captureModifier = s.Capture.Modifier;
        bool allowBareBacktick = s.Capture.AllowBareBacktick;

        s.Voice = new VoiceSettings { Shortcut = voiceShortcut };
        s.Capture = new CaptureSettings
        {
            Enabled = captureEnabled,
            Modifier = captureModifier,
            AllowBareBacktick = allowBareBacktick,
        };
        s.Ui = new UiSettings();
        _settings.Save();
        Load();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            "恢复语音输入与截屏的默认设置？快捷键与常规设置不会改变。",
            "FireNeb DevEnv",
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
            MessageBox.Show(error ?? "保存失败。", "FireNeb DevEnv",
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
