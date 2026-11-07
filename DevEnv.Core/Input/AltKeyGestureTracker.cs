namespace DevEnv.Core.Input;

/// <summary>一次按键事件的处理结论。</summary>
/// <param name="Trigger">是否触发全局启动器（只在单独按下左 Alt 并抬起时为 true）。</param>
public readonly record struct AltKeyGestureResult(bool Trigger);

/// <summary>
/// 左 Alt 手势识别：判断「单独按一下左 Alt」与「Alt+其它键的组合键」。
///
/// 规则：
/// - 左 Alt 按下 → 记下手势，暂不吞键；
/// - 右 Alt 或其它键按下 → 视为组合键，本次手势不再触发；
/// - 左 Alt 抬起时，若期间没有按过其它键 → 触发全局启动器。
///
/// 抬起**不吞**：Alt 抬起必须原样交给系统，前台程序才不会认为 Alt 一直按着，
/// 否则之后任何按键都会被当成 Alt+组合键。右 Alt（AltGr、语音键）永不触发。
/// 纯逻辑、不碰 Win32，方便单元测试。
/// </summary>
public sealed class AltKeyGestureTracker
{
    public const int VkLeftAlt = 0xA4;
    public const int VkRightAlt = 0xA5;

    /// <summary>通用 Alt 虚拟键；部分场景（注入按键、部分键盘布局）抬起时会报这个码。</summary>
    public const int VkAlt = 0x12;

    private bool _altDown;
    private bool _combo;

    public static bool IsLeftAlt(int virtualKey) => virtualKey == VkLeftAlt;

    public static bool IsRightAlt(int virtualKey) => virtualKey == VkRightAlt;

    /// <summary>是否是任意一种 Alt 抬起码（左 / 右 / 通用）。</summary>
    public static bool IsAltKey(int virtualKey) =>
        virtualKey is VkLeftAlt or VkRightAlt or VkAlt;

    /// <summary>当前是否正处在一次「左 Alt 按下、尚未抬起」的手势里。</summary>
    public bool IsAltHeld => _altDown;

    /// <summary>
    /// 按键按下。返回值恒为「不触发」：按下阶段还没法确定是不是组合键起手，
    /// 过早判定会让 Alt+Tab / Alt+F4 之类的组合失效。
    /// </summary>
    public AltKeyGestureResult OnKeyDown(int virtualKey)
    {
        if (IsLeftAlt(virtualKey))
        {
            if (!_altDown)
            {
                _altDown = true;
                _combo = false;
            }
            // 同一侧 Alt 的按住自动重复不算组合键，保持原判断
            return default;
        }

        if (_altDown)
            _combo = true;
        return default;
    }

    /// <summary>
    /// 按键抬起。只有「单独按了一下左 Alt 键」才返回触发。
    /// 抬起可能是左 / 右 / 通用 Alt 三种码，都用来结束这次手势，避免手势状态残留。
    /// </summary>
    public AltKeyGestureResult OnKeyUp(int virtualKey)
    {
        if (!_altDown || !IsAltKey(virtualKey))
            return default;

        var result = _combo
            ? default
            : new AltKeyGestureResult(Trigger: true);
        Reset();
        return result;
    }

    /// <summary>清空手势状态（禁用钩子、暂停拦截时调用，避免残留半截状态）。</summary>
    public void Reset()
    {
        _altDown = false;
        _combo = false;
    }
}
