using System.Runtime.InteropServices;
using DevEnv.Core.Input;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 全局热键：显示窗口 / 熔岩剪贴板 / 熔岩翻译。组合键来自 data.json，可在「设置 → 快捷键」修改。
/// 纯 P/Invoke，与 UI 框架无关；WPF 下由 <c>HwndSource.AddHook</c> 接管 <c>WM_HOTKEY</c>。
/// </summary>
internal sealed class GlobalHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int IdShowWindow = 0xD0B0;
    private const int IdClipboard = 0x4C42;
    private const int IdTranslator = 0x4C43;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _hwnd;
    private string _showHotkey = "";
    private string _clipboardHotkey = "";
    private string _translatorHotkey = "";
    private bool _showOk;
    private bool _suspended;

    public event Action? ShowWindowPressed;
    public event Action? ClipboardPressed;
    public event Action? TranslatorPressed;

    public GlobalHotkeys(IntPtr hwnd) => _hwnd = hwnd;

    public string ShowHotkey => _showHotkey;
    public string ClipboardHotkey => _clipboardHotkey;
    public string TranslatorHotkey => _translatorHotkey;
    public bool ShowWindowRegistered => _showOk;
    public bool ClipboardRegistered { get; private set; }
    public bool TranslatorRegistered { get; private set; }

    /// <summary>窗口句柄重建后调用，把热键绑到新 hwnd。</summary>
    public void Rebind(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        UnregisterAll();
        _hwnd = hwnd;
    }

    public bool ApplyShowWindowHotkey(string? hotkey)
    {
        Unregister(IdShowWindow);
        _showHotkey = "";
        _showOk = false;
        if (_hwnd == IntPtr.Zero || _suspended || string.IsNullOrWhiteSpace(hotkey))
            return false;
        if (!HotkeyParser.TryParse(hotkey, out HotkeyGesture gesture)
            || !HotkeyValidator.Validate(gesture).IsValid)
        {
            return false;
        }

        // 不加 MOD_NOREPEAT：部分环境下会导致注册失败（沿用历史行为）
        _showOk = RegisterHotKey(_hwnd, IdShowWindow, (uint)gesture.Modifiers, (uint)gesture.VirtualKey);
        if (_showOk)
            _showHotkey = HotkeyParser.FormatCanonical(gesture);
        return _showOk;
    }

    /// <summary>全局唤起熔岩剪贴板。注册失败说明组合键已被其它程序占用或格式非法。</summary>
    public bool ApplyClipboardHotkey(string? hotkey)
    {
        Unregister(IdClipboard);
        _clipboardHotkey = "";
        ClipboardRegistered = false;
        if (_hwnd == IntPtr.Zero || _suspended || string.IsNullOrWhiteSpace(hotkey))
            return false;
        if (!HotkeyParser.TryParse(hotkey, out HotkeyGesture gesture)
            || !HotkeyValidator.Validate(gesture).IsValid)
        {
            return false;
        }

        ClipboardRegistered = RegisterHotKey(_hwnd, IdClipboard, (uint)gesture.Modifiers, (uint)gesture.VirtualKey);
        if (ClipboardRegistered)
            _clipboardHotkey = HotkeyParser.FormatCanonical(gesture);
        return ClipboardRegistered;
    }

    /// <summary>全局唤起熔岩翻译。注册失败说明组合键已被其它程序占用或格式非法。</summary>
    public bool ApplyTranslatorHotkey(string? hotkey)
    {
        Unregister(IdTranslator);
        _translatorHotkey = "";
        TranslatorRegistered = false;
        if (_hwnd == IntPtr.Zero || _suspended || string.IsNullOrWhiteSpace(hotkey))
            return false;
        if (!HotkeyParser.TryParse(hotkey, out HotkeyGesture gesture)
            || !HotkeyValidator.Validate(gesture).IsValid)
        {
            return false;
        }

        TranslatorRegistered = RegisterHotKey(_hwnd, IdTranslator, (uint)gesture.Modifiers, (uint)gesture.VirtualKey);
        if (TranslatorRegistered)
            _translatorHotkey = HotkeyParser.FormatCanonical(gesture);
        return TranslatorRegistered;
    }

    /// <summary>录制快捷键期间注销全部热键，避免旧组合键抢先触发。</summary>
    public void Suspend()
    {
        _suspended = true;
        UnregisterAll();
    }

    /// <summary>结束挂起；调用方随后应重新应用当前配置的热键。</summary>
    public void Resume() => _suspended = false;

    /// <summary>挂到 <c>HwndSource.AddHook</c>；处理掉 WM_HOTKEY 并返回 true。</summary>
    public bool HandleMessage(int msg, IntPtr wParam)
    {
        if (msg != WmHotkey || _suspended)
            return false;
        var id = wParam.ToInt32();
        if (id == IdShowWindow)
        {
            ShowWindowPressed?.Invoke();
            return true;
        }
        if (id == IdClipboard)
        {
            ClipboardPressed?.Invoke();
            return true;
        }
        if (id == IdTranslator)
        {
            TranslatorPressed?.Invoke();
            return true;
        }
        return false;
    }

    public void Dispose() => UnregisterAll();

    private void UnregisterAll()
    {
        Unregister(IdShowWindow);
        Unregister(IdClipboard);
        Unregister(IdTranslator);
        _showOk = false;
        ClipboardRegistered = false;
        TranslatorRegistered = false;
    }

    private void Unregister(int id)
    {
        if (_hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, id);
    }
}
