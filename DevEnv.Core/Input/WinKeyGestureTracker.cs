namespace DevEnv.Core.Input;

/// <summary>一次按键事件的处理结论。</summary>
/// <param name="Swallow">是否吞掉这次按键（不交给系统处理）。</param>
/// <param name="Trigger">是否触发启动台（只在单独按下 Win 键并抬起时为 true）。</param>
public readonly record struct WinKeyGestureResult(bool Swallow, bool Trigger);

/// <summary>
/// Win 键手势识别：判断「单独按一下 Win 键」与「Win+其它键的组合键」。
///
/// 规则：
/// - Win 按下 → 记下是哪一侧的 Win，暂不吞键；
/// - 其它键按下（或另一侧 Win 也按下）→ 视为组合键，本次手势不再触发；
/// - Win 抬起时，若期间没有按过其它键 → 吞掉这次抬起并触发启动台。
///
/// 之所以只吞抬起：Windows 的开始菜单是在 Win 键**抬起**时才弹出的，
/// 吞掉抬起即可阻止开始菜单，同时 Win+E / Win+R / Win+D 等组合键完全不受影响。
/// 纯逻辑、不碰 Win32，方便单元测试。
/// </summary>
public sealed class WinKeyGestureTracker
{
    public const int VkLeftWin = 0x5B;
    public const int VkRightWin = 0x5C;

    private bool _winDown;
    private int _winKey;
    private bool _combo;

    public static bool IsWinKey(int virtualKey) => virtualKey is VkLeftWin or VkRightWin;

    /// <summary>当前是否正处在一次「Win 按下、尚未抬起」的手势里。</summary>
    public bool IsWinHeld => _winDown;

    /// <summary>
    /// 按键按下。返回值恒为「不吞键」：按下阶段还没法确定是不是组合键起手，
    /// 过早吞掉会让 Win+E 之类的组合失效。
    /// </summary>
    public WinKeyGestureResult OnKeyDown(int virtualKey)
    {
        if (IsWinKey(virtualKey))
        {
            if (!_winDown)
            {
                _winDown = true;
                _winKey = virtualKey;
                _combo = false;
            }
            else if (virtualKey != _winKey)
            {
                // 左右 Win 同时按：当成组合键，避免误弹启动台
                _combo = true;
            }
            // 同一侧 Win 的按住自动重复不算组合键，保持原判断
            return default;
        }

        if (_winDown)
            _combo = true;
        return default;
    }

    /// <summary>按键抬起。只有「单独按了一下 Win 键」才返回触发 + 吞键。</summary>
    public WinKeyGestureResult OnKeyUp(int virtualKey)
    {
        if (!_winDown || !IsWinKey(virtualKey))
            return default;

        var result = _combo
            ? default
            : new WinKeyGestureResult(Swallow: true, Trigger: true);
        Reset();
        return result;
    }

    /// <summary>清空手势状态（禁用钩子、暂停拦截时调用，避免残留半截状态）。</summary>
    public void Reset()
    {
        _winDown = false;
        _winKey = 0;
        _combo = false;
    }
}
