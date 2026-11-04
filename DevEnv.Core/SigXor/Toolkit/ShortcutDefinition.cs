namespace DevEnv.Core.SigXor.Toolkit;

public static class ShortcutDefinition
{
    public const string RightAlt = "right-alt";
    public const string LeftAlt = "left-alt";
    public const string RightCtrl = "right-ctrl";
    public const string LeftCtrl = "left-ctrl";
    public const string CapsLock = "caps-lock";

    public const string CaptureAlt = "alt";
    public const string CaptureCtrl = "ctrl";
    public const string CaptureCtrlShift = "ctrl+shift";
    public const string CaptureWin = "win";

    public const int VkLeftAlt = 0xA4;
    public const int VkRightAlt = 0xA5;
    public const int VkLeftCtrl = 0xA2;
    public const int VkRightCtrl = 0xA3;
    public const int VkCapsLock = 0x14;

    public static string NormalizeVoiceShortcut(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "left-alt" or "lalt" or "leftalt" => LeftAlt,
            "right-ctrl" or "rctrl" or "rightctrl" => RightCtrl,
            "left-ctrl" or "lctrl" or "leftctrl" => LeftCtrl,
            "caps-lock" or "capslock" or "caps" => CapsLock,
            _ => RightAlt,
        };

    public static string FormatVoiceShortcut(string? value) =>
        NormalizeVoiceShortcut(value) switch
        {
            LeftAlt => "左 Alt",
            RightCtrl => "右 Ctrl",
            LeftCtrl => "左 Ctrl",
            CapsLock => "Caps Lock",
            _ => "右 Alt",
        };

    public static int ToVoiceVirtualKey(string? value) =>
        NormalizeVoiceShortcut(value) switch
        {
            LeftAlt => VkLeftAlt,
            RightCtrl => VkRightCtrl,
            LeftCtrl => VkLeftCtrl,
            CapsLock => VkCapsLock,
            _ => VkRightAlt,
        };

    public static string NormalizeCaptureModifier(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "ctrl" or "control" => CaptureCtrl,
            "ctrl+shift" or "control+shift" => CaptureCtrlShift,
            "win" or "windows" or "meta" => CaptureWin,
            _ => CaptureAlt,
        };

    public static string FormatCaptureModifier(
        string? modifier,
        string? voiceShortcut = null,
        bool allowBareBacktick = false)
    {
        if (allowBareBacktick) return "`";

        string voice = NormalizeVoiceShortcut(voiceShortcut);
        return NormalizeCaptureModifier(modifier) switch
        {
            CaptureCtrl => voice switch
            {
                LeftCtrl => "右 Ctrl + `",
                RightCtrl => "左 Ctrl + `",
                _ => "Ctrl + `",
            },
            CaptureCtrlShift => voice switch
            {
                LeftCtrl => "右 Ctrl + Shift + `",
                RightCtrl => "左 Ctrl + Shift + `",
                _ => "Ctrl + Shift + `",
            },
            CaptureWin => "Win + `",
            _ => voice == LeftAlt ? "右 Alt + `" : "左 Alt + `",
        };
    }

    public static bool IsCaptureModifier(string? value, string expected) =>
        NormalizeCaptureModifier(value) == NormalizeCaptureModifier(expected);
}
