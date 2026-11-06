using System.Globalization;

namespace DevEnv.Core.Input;

/// <summary>
/// 组合键文本解析与格式化。持久化格式示例：<c>Ctrl+Alt+D</c>、<c>Alt+Space</c>、<c>Ctrl+Shift+`</c>。
/// 纯逻辑，不依赖 WPF，主程序热键与设置页录制控件共用。
/// </summary>
public static class HotkeyParser
{
    private static readonly Dictionary<string, int> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20,
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Return"] = 0x0D,
        ["Esc"] = 0x1B,
        ["Escape"] = 0x1B,
        ["Backspace"] = 0x08,
        ["Back"] = 0x08,
        ["Delete"] = 0x2E,
        ["Del"] = 0x2E,
        ["Insert"] = 0x2D,
        ["Ins"] = 0x2D,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PgUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["PgDn"] = 0x22,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["PrintScreen"] = 0x2C,
        ["PrtSc"] = 0x2C,
        ["Pause"] = 0x13,
        ["CapsLock"] = 0x14,
        ["NumLock"] = 0x90,
        ["ScrollLock"] = 0x91,
        ["Oem3"] = 0xC0,
        ["OemMinus"] = 0xBD,
        ["OemPlus"] = 0xBB,
        ["OemOpenBrackets"] = 0xDB,
        ["Oem6"] = 0xDD,
        ["Oem5"] = 0xDC,
        ["Oem1"] = 0xBA,
        ["OemQuotes"] = 0xDE,
        ["OemComma"] = 0xBC,
        ["OemPeriod"] = 0xBE,
        ["OemQuestion"] = 0xBF,
        ["Multiply"] = 0x6A,
        ["Add"] = 0x6B,
        ["Subtract"] = 0x6D,
        ["Decimal"] = 0x6E,
        ["Divide"] = 0x6F,
    };

    private static readonly Dictionary<int, string> CanonicalNames = BuildCanonicalNames();

    private static Dictionary<int, string> BuildCanonicalNames()
    {
        var map = new Dictionary<int, string>
        {
            [0x20] = "Space",
            [0x09] = "Tab",
            [0x0D] = "Enter",
            [0x1B] = "Esc",
            [0x08] = "Backspace",
            [0x2E] = "Delete",
            [0x2D] = "Insert",
            [0x24] = "Home",
            [0x23] = "End",
            [0x21] = "PageUp",
            [0x22] = "PageDown",
            [0x25] = "Left",
            [0x26] = "Up",
            [0x27] = "Right",
            [0x28] = "Down",
            [0x2C] = "PrintScreen",
            [0x13] = "Pause",
            [0x14] = "CapsLock",
            [0x90] = "NumLock",
            [0x91] = "ScrollLock",
            [0xC0] = "`",
            [0xBD] = "-",
            [0xBB] = "=",
            [0xDB] = "[",
            [0xDD] = "]",
            [0xDC] = "\\",
            [0xBA] = ";",
            [0xDE] = "'",
            [0xBC] = ",",
            [0xBE] = ".",
            [0xBF] = "/",
            [0x6A] = "Multiply",
            [0x6B] = "Add",
            [0x6D] = "Subtract",
            [0x6E] = "Decimal",
            [0x6F] = "Divide",
        };

        for (int i = 0; i < 10; i++)
        {
            map[0x30 + i] = ((char)('0' + i)).ToString();
            map[0x60 + i] = "Num" + i.ToString(CultureInfo.InvariantCulture);
        }
        for (int i = 0; i < 26; i++)
            map[0x41 + i] = ((char)('A' + i)).ToString();
        for (int i = 0; i < 24; i++)
            map[0x70 + i] = "F" + (i + 1).ToString(CultureInfo.InvariantCulture);

        return map;
    }

    /// <summary>解析组合键字符串。不要求包含修饰键，修饰键要求由 <see cref="HotkeyValidator"/> 判断。</summary>
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var modifiers = HotkeyModifiers.None;
        string? keyToken = null;

        foreach (string raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IsModifierName(raw, out var modifier))
            {
                modifiers |= modifier;
                continue;
            }

            // 只允许一个非修饰键
            if (keyToken is not null)
                return false;
            keyToken = raw;
        }

        if (keyToken is null || !TryParseKey(keyToken, out int virtualKey))
            return false;

        gesture = new HotkeyGesture(modifiers, virtualKey);
        return true;
    }

    /// <summary>解析单个键名（不含修饰键），例如 Space / F12 / D / `。</summary>
    public static bool TryParseKey(string? token, out int virtualKey)
    {
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        string value = token.Trim();
        if (NamedKeys.TryGetValue(value, out virtualKey))
            return true;

        if (value.Length == 1)
        {
            char c = value[0];
            if (c is >= 'a' and <= 'z')
            {
                virtualKey = char.ToUpperInvariant(c);
                return true;
            }
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = c;
                return true;
            }

            virtualKey = c switch
            {
                '`' => 0xC0,
                '-' => 0xBD,
                '=' => 0xBB,
                '[' => 0xDB,
                ']' => 0xDD,
                '\\' => 0xDC,
                ';' => 0xBA,
                '\'' => 0xDE,
                ',' => 0xBC,
                '.' => 0xBE,
                '/' => 0xBF,
                _ => 0,
            };
            return virtualKey != 0;
        }

        if (value.Length is >= 2 and <= 3
            && (value[0] == 'F' || value[0] == 'f')
            && int.TryParse(value.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int function)
            && function is >= 1 and <= 24)
        {
            virtualKey = 0x70 + function - 1;
            return true;
        }

        if (value.StartsWith("Num", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value.AsSpan(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out int numpad)
            && numpad is >= 0 and <= 9)
        {
            virtualKey = 0x60 + numpad;
            return true;
        }

        return false;
    }

    /// <summary>持久化 / 展示统一的组合键文本，例如 <c>Ctrl+Alt+D</c>。</summary>
    public static string FormatCanonical(HotkeyGesture gesture)
    {
        if (gesture.IsEmpty)
            return string.Empty;

        var parts = new List<string>(5);
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(GetKeyName(gesture.VirtualKey));
        return string.Join('+', parts);
    }

    /// <summary>与 <see cref="FormatCanonical"/> 相同，保留一个短名称便于调用。</summary>
    public static string Format(HotkeyGesture gesture) => FormatCanonical(gesture);

    public static string GetKeyName(int virtualKey) =>
        CanonicalNames.TryGetValue(virtualKey, out string? name) ? name : $"0x{virtualKey:X2}";

    public static bool TryGetKeyName(int virtualKey, out string name)
    {
        if (CanonicalNames.TryGetValue(virtualKey, out string? found))
        {
            name = found;
            return true;
        }

        name = string.Empty;
        return false;
    }

    public static string GetDisplayName(int virtualKey) => GetKeyName(virtualKey);

    public static bool IsModifierKey(int virtualKey) =>
        virtualKey is 0x10 or 0x11 or 0x12          // Shift / Control / Alt
            or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5
            or 0x5B or 0x5C;                        // 左右 Win

    private static bool IsModifierName(string token, out HotkeyModifiers modifier)
    {
        modifier = token.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => HotkeyModifiers.Control,
            "ALT" => HotkeyModifiers.Alt,
            "SHIFT" => HotkeyModifiers.Shift,
            "WIN" or "WINDOWS" => HotkeyModifiers.Windows,
            _ => HotkeyModifiers.None,
        };
        return modifier != HotkeyModifiers.None;
    }
}
