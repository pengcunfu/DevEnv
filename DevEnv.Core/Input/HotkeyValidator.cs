namespace DevEnv.Core.Input;

/// <summary>单个快捷键的校验结果。</summary>
public readonly record struct HotkeyValidationResult(bool IsValid, string? Message)
{
    public static HotkeyValidationResult Ok => new(true, null);
    public static HotkeyValidationResult Fail(string message) => new(false, message);
}

/// <summary>批量校验时某一条快捷键的错误。</summary>
public readonly record struct HotkeyBindingError(string Id, string Message);

/// <summary>全局热键校验：修饰键、系统保留组合与重复绑定。</summary>
public static class HotkeyValidator
{
    private const int VkTab = 0x09;
    private const int VkEscape = 0x1B;
    private const int VkDelete = 0x2E;
    private const int VkF4 = 0x73;
    private const int VkD = 0x44;
    private const int VkE = 0x45;
    private const int VkL = 0x4C;
    private const int VkR = 0x52;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;

    /// <summary>校验一条全局组合键。</summary>
    public static HotkeyValidationResult Validate(HotkeyGesture gesture)
    {
        if (gesture.IsEmpty)
            return HotkeyValidationResult.Fail("请按下快捷键。");

        if (HotkeyParser.IsModifierKey(gesture.VirtualKey))
            return HotkeyValidationResult.Fail("快捷键必须包含一个非修饰键。");

        bool hasMainModifier =
            gesture.Modifiers.HasFlag(HotkeyModifiers.Control)
            || gesture.Modifiers.HasFlag(HotkeyModifiers.Alt)
            || gesture.Modifiers.HasFlag(HotkeyModifiers.Windows);
        if (!hasMainModifier)
            return HotkeyValidationResult.Fail("快捷键至少包含 Ctrl、Alt、Win 之一。");

        if (IsReserved(gesture))
            return HotkeyValidationResult.Fail($"{HotkeyParser.FormatCanonical(gesture)} 是系统保留组合键，请换一个。");

        return HotkeyValidationResult.Ok;
    }

    /// <summary>校验多条快捷键，并检测完全相同的组合。</summary>
    public static IReadOnlyList<HotkeyBindingError> ValidateBindings(
        IReadOnlyList<(string Id, string? Text)> bindings)
    {
        var errors = new List<HotkeyBindingError>();
        var parsed = new List<(string Id, HotkeyGesture Gesture)>();

        foreach (var (id, text) in bindings)
        {
            if (!HotkeyParser.TryParse(text, out HotkeyGesture gesture))
            {
                errors.Add(new HotkeyBindingError(id, "快捷键格式不正确。"));
                continue;
            }

            HotkeyValidationResult result = Validate(gesture);
            if (!result.IsValid)
            {
                errors.Add(new HotkeyBindingError(id, result.Message ?? "快捷键无效。"));
                continue;
            }

            parsed.Add((id, gesture));
        }

        errors.AddRange(ValidateUnique(parsed));
        return errors;
    }

    /// <summary>检测重复的组合键；后出现的条目会收到错误。</summary>
    public static IReadOnlyList<HotkeyBindingError> ValidateUnique(
        IReadOnlyList<(string Id, HotkeyGesture Gesture)> bindings)
    {
        var errors = new List<HotkeyBindingError>();
        var seen = new Dictionary<(HotkeyModifiers, int), string>();

        foreach (var (id, gesture) in bindings)
        {
            var key = (gesture.Modifiers, gesture.VirtualKey);
            if (seen.TryGetValue(key, out string? firstId))
            {
                errors.Add(new HotkeyBindingError(id, $"与「{firstId}」使用了相同的快捷键。"));
                continue;
            }
            seen[key] = id;
        }

        return errors;
    }

    private static bool IsReserved(HotkeyGesture gesture)
    {
        HotkeyModifiers mods = gesture.Modifiers;
        int vk = gesture.VirtualKey;

        if (mods == HotkeyModifiers.Alt && vk is VkTab or VkEscape or VkF4)
            return true;
        if (mods == (HotkeyModifiers.Alt | HotkeyModifiers.Shift) && vk == VkTab)
            return true;
        if (mods == HotkeyModifiers.Control && vk == VkEscape)
            return true;
        if (mods == (HotkeyModifiers.Control | HotkeyModifiers.Shift) && vk == VkEscape)
            return true;
        if (mods == (HotkeyModifiers.Control | HotkeyModifiers.Alt) && vk == VkDelete)
            return true;

        // Win 组合大多由系统 Shell 先处理，允许注册也没有实际意义；这里直接拒绝常见项。
        if (mods.HasFlag(HotkeyModifiers.Windows)
            && vk is VkD or VkE or VkL or VkR or VkTab or VkLeft or VkUp or VkRight or VkDown)
        {
            return true;
        }

        return false;
    }
}
