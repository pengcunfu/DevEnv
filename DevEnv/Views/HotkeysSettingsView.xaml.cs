using System.Windows;
using System.Windows.Controls;
using DevEnv.Core.Input;
using DevEnv.Core.Services;
using DevEnv.Core.SigXor.Toolkit;
using DevEnv.Host.SigXor.Services;
using HostSettingsService = DevEnv.Core.Services.SettingsService;
using SigXorSettingsService = DevEnv.Host.SigXor.Services.SettingsService;

namespace DevEnv.Host.Views;

/// <summary>「设置 → 快捷键」页：集中修改程序绑定的全部快捷键。</summary>
public partial class HotkeysSettingsView : UserControl
{
    private readonly SigXorSettingsService _settings;
    private readonly HostSettingsService _hostSettings;
    private readonly Action? _shortcutsSaved;

    public HotkeysSettingsView(SigXorSettingsService settings, Action? shortcutsSaved = null)
        : this(settings, CoreServices.Settings, shortcutsSaved)
    {
    }

    public HotkeysSettingsView(
        SigXorSettingsService settings,
        HostSettingsService hostSettings,
        Action? shortcutsSaved = null)
    {
        InitializeComponent();
        _settings = settings;
        _hostSettings = hostSettings;
        _shortcutsSaved = shortcutsSaved;

        InitializeOptions();
        Loaded += (_, _) => Reload();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) Reload();
        };
        VoiceShortcutCombo.SelectionChanged += (_, _) => UpdateCaptureHint();
        CaptureModifierCombo.SelectionChanged += (_, _) => UpdateCaptureHint();
        AllowBareBacktickCheck.Checked += (_, _) => UpdateCaptureHint();
        AllowBareBacktickCheck.Unchecked += (_, _) => UpdateCaptureHint();

        Reload();
    }

    /// <summary>当前快捷键说明，供「服务与模型」页复用。</summary>
    public string ShortcutHint
    {
        get
        {
            var s = _settings.Current;
            return $"语音：{ShortcutDefinition.FormatVoiceShortcut(s.Voice.Shortcut)}；"
                 + $"截屏：{ShortcutDefinition.FormatCaptureModifier(s.Capture.Modifier, s.Voice.Shortcut, s.Capture.AllowBareBacktick)}；"
                 + $"文件预览：{ShortcutDefinition.FormatPreviewShortcut(s.Preview.OpenShortcut)}；"
                 + $"关闭预览：{ShortcutDefinition.FormatPreviewShortcut(s.Preview.CloseShortcut)}。";
        }
    }

    /// <summary>取消正在进行的录制（设置窗口按 Esc 或关闭时调用）。</summary>
    public void CancelCapture()
    {
        ShowWindowRecorder.CancelCapture();
        QuickAppRecorder.CancelCapture();
    }

    private void InitializeOptions()
    {
        VoiceShortcutCombo.ItemsSource = new[]
        {
            new ShortcutOption(ShortcutDefinition.RightAlt, "右 Alt（默认）"),
            new ShortcutOption(ShortcutDefinition.LeftAlt, "左 Alt"),
            new ShortcutOption(ShortcutDefinition.RightCtrl, "右 Ctrl"),
            new ShortcutOption(ShortcutDefinition.LeftCtrl, "左 Ctrl"),
            new ShortcutOption(ShortcutDefinition.CapsLock, "Caps Lock"),
        };
        VoiceShortcutCombo.DisplayMemberPath = nameof(ShortcutOption.Label);
        VoiceShortcutCombo.SelectedValuePath = nameof(ShortcutOption.Value);

        CaptureModifierCombo.ItemsSource = new[]
        {
            new ShortcutOption(ShortcutDefinition.CaptureAlt, "Alt + `"),
            new ShortcutOption(ShortcutDefinition.CaptureCtrl, "Ctrl + `"),
            new ShortcutOption(ShortcutDefinition.CaptureCtrlShift, "Ctrl + Shift + `"),
            new ShortcutOption(ShortcutDefinition.CaptureWin, "Win + `"),
        };
        CaptureModifierCombo.DisplayMemberPath = nameof(ShortcutOption.Label);
        CaptureModifierCombo.SelectedValuePath = nameof(ShortcutOption.Value);

        var previewOptions = ShortcutDefinition.PreviewKeyNames
            .Select(key => new ShortcutOption(key, ShortcutDefinition.FormatPreviewShortcut(key)))
            .ToArray();
        PreviewOpenCombo.ItemsSource = previewOptions;
        PreviewOpenCombo.DisplayMemberPath = nameof(ShortcutOption.Label);
        PreviewOpenCombo.SelectedValuePath = nameof(ShortcutOption.Value);
        PreviewCloseCombo.ItemsSource = previewOptions;
        PreviewCloseCombo.DisplayMemberPath = nameof(ShortcutOption.Label);
        PreviewCloseCombo.SelectedValuePath = nameof(ShortcutOption.Value);
    }

    /// <summary>重新读取两个配置文件（切回本页签时避免显示旧值）。</summary>
    public void Reload()
    {
        CancelCapture();

        var cfg = _hostSettings.GetConfig();
        ShowWindowRecorder.Hotkey = NormalizeHotkey(cfg.ShowWindowHotkey, "Ctrl+Alt+D");
        QuickAppRecorder.Hotkey = NormalizeHotkey(cfg.QuickAppHotkey, "Alt+Space");
        WinKeyLauncherCheck.IsChecked = cfg.WinKeyLauncher;

        var s = _settings.Current;
        VoiceShortcutCombo.SelectedValue = ShortcutDefinition.NormalizeVoiceShortcut(s.Voice.Shortcut);
        CaptureEnabledCheck.IsChecked = s.Capture.Enabled;
        CaptureModifierCombo.SelectedValue = ShortcutDefinition.NormalizeCaptureModifier(s.Capture.Modifier);
        AllowBareBacktickCheck.IsChecked = s.Capture.AllowBareBacktick;
        PreviewOpenCombo.SelectedValue = ShortcutDefinition.NormalizePreviewShortcut(
            s.Preview.OpenShortcut, ShortcutDefinition.PreviewSpace);
        PreviewCloseCombo.SelectedValue = ShortcutDefinition.NormalizePreviewShortcut(
            s.Preview.CloseShortcut, ShortcutDefinition.PreviewEscape);

        UpdateCaptureHint();
        Message.Text = "";
    }

    private static string NormalizeHotkey(string? text, string fallback) =>
        HotkeyParser.TryParse(text, out HotkeyGesture gesture)
            ? HotkeyParser.FormatCanonical(gesture)
            : fallback;

    private void UpdateCaptureHint()
    {
        if (CaptureHintText is null) return;

        string voice = ShortcutDefinition.NormalizeVoiceShortcut(VoiceShortcutCombo.SelectedValue as string);
        string modifier = ShortcutDefinition.NormalizeCaptureModifier(CaptureModifierCombo.SelectedValue as string);
        bool allowBare = AllowBareBacktickCheck.IsChecked == true;
        string effective = ShortcutDefinition.FormatCaptureModifier(modifier, voice, allowBare);
        CaptureHintText.Text = $"当前截屏快捷键：{effective}";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TrySave(out string? error))
        {
            Message.Text = error ?? "保存失败";
            return;
        }

        Message.Text = "已保存";
    }

    private bool TrySave(out string? error)
    {
        CancelCapture();

        var errors = HotkeyValidator.ValidateBindings(
        [
            ("显示主窗口", ShowWindowRecorder.Hotkey),
            ("快捷应用", QuickAppRecorder.Hotkey),
        ]);
        if (errors.Count > 0)
        {
            error = errors[0].Message;
            return false;
        }

        string open = ShortcutDefinition.NormalizePreviewShortcut(
            PreviewOpenCombo.SelectedValue as string, ShortcutDefinition.PreviewSpace);
        string close = ShortcutDefinition.NormalizePreviewShortcut(
            PreviewCloseCombo.SelectedValue as string, ShortcutDefinition.PreviewEscape);
        if (string.Equals(open, close, StringComparison.OrdinalIgnoreCase))
        {
            error = "打开预览与关闭预览不能使用同一个按键。";
            return false;
        }

        if (AllowBareBacktickCheck.IsChecked == true
            && (open == "`" || close == "`"))
        {
            error = "文件预览键与「允许无修饰 ` 触发截屏」冲突，请换一个按键或关闭该选项。";
            return false;
        }

        var hostResult = _hostSettings.SaveHotkeys(
            ShowWindowRecorder.Hotkey,
            QuickAppRecorder.Hotkey,
            WinKeyLauncherCheck.IsChecked == true);
        if (!hostResult.Ok)
        {
            error = hostResult.DisplayMessage;
            return false;
        }

        var s = _settings.Current;
        s.Voice.Shortcut = ShortcutDefinition.NormalizeVoiceShortcut(VoiceShortcutCombo.SelectedValue as string);
        s.Capture.Enabled = CaptureEnabledCheck.IsChecked == true;
        s.Capture.Modifier = ShortcutDefinition.NormalizeCaptureModifier(CaptureModifierCombo.SelectedValue as string);
        s.Capture.AllowBareBacktick = AllowBareBacktickCheck.IsChecked == true;
        s.Preview.OpenShortcut = open;
        s.Preview.CloseShortcut = close;
        _settings.Save();

        UpdateCaptureHint();
        _shortcutsSaved?.Invoke();
        error = null;
        return true;
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            "恢复所有快捷键的默认值？不会改动语音引擎、识别语言等非快捷键设置。",
            "燧岚开发盒子",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        ShowWindowRecorder.Hotkey = "Ctrl+Alt+D";
        QuickAppRecorder.Hotkey = "Alt+Space";
        WinKeyLauncherCheck.IsChecked = true;
        VoiceShortcutCombo.SelectedValue = ShortcutDefinition.RightAlt;
        CaptureEnabledCheck.IsChecked = true;
        CaptureModifierCombo.SelectedValue = ShortcutDefinition.CaptureAlt;
        AllowBareBacktickCheck.IsChecked = false;
        PreviewOpenCombo.SelectedValue = ShortcutDefinition.PreviewSpace;
        PreviewCloseCombo.SelectedValue = ShortcutDefinition.PreviewEscape;
        UpdateCaptureHint();

        Message.Text = TrySave(out string? error) ? "已恢复默认" : error ?? "恢复默认失败";
    }

    private sealed record ShortcutOption(string Value, string Label);
}
