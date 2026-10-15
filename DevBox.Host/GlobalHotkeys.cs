using System.Runtime.InteropServices;

namespace DevBox.Host;

/// <summary>WinForms 全局热键：显示窗口 / Alt+Space 翻译 / Ctrl+Space 剪贴板。</summary>
internal sealed class GlobalHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int IdShowWindow = 0xD0B0;
    private const int IdTranslator = 0x4C41;
    private const int IdClipboard = 0x4342;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint VkSpace = 0x20;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _hwnd;
    private string _showHotkey = "";
    private bool _translatorOk;
    private bool _clipboardOk;
    private bool _showOk;

    public event Action? ShowWindowPressed;
    public event Action? TranslatorPressed;
    public event Action? ClipboardPressed;

    public GlobalHotkeys(IntPtr hwnd) => _hwnd = hwnd;

    public string ShowHotkey => _showHotkey;
    public bool TranslatorRegistered => _translatorOk;
    public bool ClipboardRegistered => _clipboardOk;
    public bool ShowWindowRegistered => _showOk;

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
        if (_hwnd == IntPtr.Zero || string.IsNullOrWhiteSpace(hotkey))
            return false;
        if (!TryParse(hotkey, out var mods, out var vk))
            return false;
        // 与 Avalonia 一致：不加 MOD_NOREPEAT，避免部分环境下注册失败
        _showOk = RegisterHotKey(_hwnd, IdShowWindow, mods, vk);
        if (_showOk)
            _showHotkey = hotkey;
        return _showOk;
    }

    public bool RegisterTranslatorAltSpace()
    {
        Unregister(IdTranslator);
        _translatorOk = false;
        if (_hwnd == IntPtr.Zero)
            return false;
        _translatorOk = RegisterHotKey(_hwnd, IdTranslator, ModAlt, VkSpace);
        return _translatorOk;
    }

    public bool RegisterClipboardCtrlSpace()
    {
        Unregister(IdClipboard);
        _clipboardOk = false;
        if (_hwnd == IntPtr.Zero)
            return false;
        _clipboardOk = RegisterHotKey(_hwnd, IdClipboard, ModControl, VkSpace);
        return _clipboardOk;
    }

    public bool TryHandle(ref Message m)
    {
        if (m.Msg != WmHotkey)
            return false;
        var id = m.WParam.ToInt32();
        if (id == IdShowWindow)
        {
            ShowWindowPressed?.Invoke();
            return true;
        }
        if (id == IdTranslator)
        {
            TranslatorPressed?.Invoke();
            return true;
        }
        if (id == IdClipboard)
        {
            ClipboardPressed?.Invoke();
            return true;
        }
        return false;
    }

    public void Dispose() => UnregisterAll();

    private void UnregisterAll()
    {
        Unregister(IdShowWindow);
        Unregister(IdTranslator);
        Unregister(IdClipboard);
        _showOk = false;
        _translatorOk = false;
        _clipboardOk = false;
    }

    private void Unregister(int id)
    {
        if (_hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, id);
    }

    private static bool TryParse(string text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        string? keyPart = null;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModControl;
                continue;
            }
            if (raw.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModAlt;
                continue;
            }
            if (raw.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModShift;
                continue;
            }
            if (raw.Equals("Win", StringComparison.OrdinalIgnoreCase)
                || raw.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModWin;
                continue;
            }
            keyPart = raw;
        }

        if (string.IsNullOrEmpty(keyPart))
            return false;

        virtualKey = keyPart.ToUpperInvariant() switch
        {
            "SPACE" => VkSpace,
            "TAB" => 0x09,
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            _ when keyPart.Length == 1 => char.ToUpperInvariant(keyPart[0]),
            _ => 0u,
        };
        return virtualKey != 0;
    }
}
