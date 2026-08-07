using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Input;

namespace ProgramBox.Utils
{
    /// <summary>
    /// 全局快捷键服务（基于 Win32 RegisterHotKey）。
    /// 直接挂接主窗口的窗口过程接收 WM_HOTKEY，触发事件后由主窗口显示并居中。
    /// </summary>
    public static class HotkeyService
    {
        /// <summary>
        /// 默认快捷键
        /// </summary>
        public const string DefaultHotkey = "Ctrl+Alt+D";

        private const int WM_HOTKEY = 0x0312;
        private const int GWLP_WNDPROC = -4;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private static readonly int HotkeyId = 0xD0B0;

        private static IntPtr _hwnd = IntPtr.Zero;
        private static IntPtr _originalProc = IntPtr.Zero;
        private static WndProcDelegate? _procDelegate;
        private static string _currentHotkey = string.Empty;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 用户按下全局快捷键
        /// </summary>
        public static event Action? HotkeyPressed;

        /// <summary>
        /// 当前生效的快捷键（空字符串表示未启用）
        /// </summary>
        public static string CurrentHotkey => _currentHotkey;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        /// <summary>
        /// 挂接主窗口窗口过程（重复调用不会重复挂接）
        /// </summary>
        public static void Attach(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || _procDelegate != null)
                return;

            _hwnd = hwnd;
            _procDelegate = WndProc;
            var procPtr = Marshal.GetFunctionPointerForDelegate(_procDelegate);
            _originalProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, procPtr);
        }

        /// <summary>
        /// 应用快捷键设置（空字符串或无效组合表示禁用）
        /// </summary>
        public static void ApplyHotkey(string? hotkey)
        {
            if (_hwnd == IntPtr.Zero)
                return;

            UnregisterHotKey(_hwnd, HotkeyId);
            _currentHotkey = string.Empty;

            if (string.IsNullOrWhiteSpace(hotkey))
                return;

            if (!TryParseHotkey(hotkey, out var modifiers, out var virtualKey))
                return;

            if (!RegisterHotKey(_hwnd, HotkeyId, modifiers | MOD_NOREPEAT, virtualKey))
                return;

            _currentHotkey = FormatHotkey(ToKeyModifiers(modifiers), (Key)virtualKey);
        }

        /// <summary>
        /// 解析快捷键文本（如 "Ctrl+Alt+D"、"F8"），失败返回 false
        /// </summary>
        public static bool TryParseHotkey(string text, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            Key key = Key.None;
            foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var part = raw.Trim();
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= MOD_CONTROL;
                    continue;
                }
                if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= MOD_ALT;
                    continue;
                }
                if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= MOD_SHIFT;
                    continue;
                }
                if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= MOD_WIN;
                    continue;
                }

                if (!Enum.TryParse<Key>(part, true, out var parsed) || IsModifierKey(parsed))
                    return false;

                if (key != Key.None)
                    return false;

                key = parsed;
            }

            if (key == Key.None)
                return false;

            var vk = ToVirtualKey(key);
            if (vk == 0)
                return false;

            virtualKey = vk;
            return true;
        }

        /// <summary>
        /// 将修饰键与按键格式化为标准文本（如 Ctrl+Alt+D）
        /// </summary>
        public static string FormatHotkey(KeyModifiers modifiers, Key key)
        {
            var parts = new List<string>();
            if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
            parts.Add(GetKeyDisplayName(key));
            return string.Join("+", parts);
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                HotkeyPressed?.Invoke();
                return IntPtr.Zero;
            }

            return CallWindowProc(_originalProc, hWnd, msg, wParam, lParam);
        }

        private static bool IsModifierKey(Key key)
            => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

        /// <summary>
        /// 将 Avalonia Key 转换为 Win32 虚拟键码（Avalonia 枚举值与 VK 不一致，需要显式映射）
        /// </summary>
        private static uint ToVirtualKey(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                return (uint)('A' + (key - Key.A));
            if (key >= Key.D0 && key <= Key.D9)
                return (uint)('0' + (key - Key.D0));
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return 0x60 + (uint)(key - Key.NumPad0);
            if (key >= Key.F1 && key <= Key.F24)
                return 0x70 + (uint)(key - Key.F1);

            return key switch
            {
                Key.Back => 0x08,
                Key.Tab => 0x09,
                Key.Return or Key.Enter => 0x0D,
                Key.Pause => 0x13,
                Key.CapsLock or Key.Capital => 0x14,
                Key.Escape => 0x1B,
                Key.Space => 0x20,
                Key.PageUp or Key.Prior => 0x21,
                Key.PageDown or Key.Next => 0x22,
                Key.End => 0x23,
                Key.Home => 0x24,
                Key.Left => 0x25,
                Key.Up => 0x26,
                Key.Right => 0x27,
                Key.Down => 0x28,
                Key.Snapshot or Key.PrintScreen => 0x2C,
                Key.Insert => 0x2D,
                Key.Delete => 0x2E,
                Key.Apps => 0x5D,
                Key.Sleep => 0x5F,
                Key.Multiply => 0x6A,
                Key.Add => 0x6B,
                Key.Separator => 0x6C,
                Key.Subtract => 0x6D,
                Key.Decimal => 0x6E,
                Key.Divide => 0x6F,
                Key.NumLock => 0x90,
                Key.Scroll => 0x91,
                Key.OemSemicolon or Key.Oem1 => 0xBA,
                Key.OemPlus => 0xBB,
                Key.OemComma => 0xBC,
                Key.OemMinus => 0xBD,
                Key.OemPeriod => 0xBE,
                Key.OemQuestion or Key.Oem2 => 0xBF,
                Key.OemTilde or Key.Oem3 => 0xC0,
                Key.OemOpenBrackets or Key.Oem4 => 0xDB,
                Key.OemPipe or Key.Oem5 => 0xDC,
                Key.OemCloseBrackets or Key.Oem6 => 0xDD,
                Key.OemQuotes or Key.Oem7 => 0xDE,
                Key.OemBackslash or Key.Oem102 => 0xE2,
                _ => 0
            };
        }

        private static KeyModifiers ToKeyModifiers(uint modifiers)
        {
            var result = KeyModifiers.None;
            if ((modifiers & MOD_CONTROL) != 0) result |= KeyModifiers.Control;
            if ((modifiers & MOD_ALT) != 0) result |= KeyModifiers.Alt;
            if ((modifiers & MOD_SHIFT) != 0) result |= KeyModifiers.Shift;
            if ((modifiers & MOD_WIN) != 0) result |= KeyModifiers.Meta;
            return result;
        }

        private static string GetKeyDisplayName(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
                return ((char)('0' + (key - Key.D0))).ToString();
            if (key >= Key.A && key <= Key.Z)
                return key.ToString();
            if (key >= Key.F1 && key <= Key.F24)
                return key.ToString();
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return "Num" + (key - Key.NumPad0);

            return key switch
            {
                Key.Space => "Space",
                Key.Enter => "Enter",
                Key.Tab => "Tab",
                Key.Escape => "Esc",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "PageUp",
                Key.PageDown => "PageDown",
                Key.Insert => "Insert",
                Key.Delete => "Delete",
                Key.Left => "Left",
                Key.Right => "Right",
                Key.Up => "Up",
                Key.Down => "Down",
                Key.Back => "Backspace",
                _ => key.ToString()
            };
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
    }
}
