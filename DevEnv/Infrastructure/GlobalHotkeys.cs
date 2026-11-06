using System.Runtime.InteropServices;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 全局热键：显示窗口 / Alt+Space 快捷应用。
/// 翻译与剪贴板不再占用全局快捷键，入口放在快捷应用窗口顶部。
/// 纯 P/Invoke，与 UI 框架无关；WPF 下由 <c>HwndSource.AddHook</c> 接管 <c>WM_HOTKEY</c>。
/// </summary>
internal sealed class GlobalHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int IdShowWindow = 0xD0B0;
    private const int IdLauncher = 0x4C41;
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
    private bool _showOk;

    public event Action? ShowWindowPressed;
    public event Action? LauncherPressed;

    public GlobalHotkeys(IntPtr hwnd) => _hwnd = hwnd;

    public string ShowHotkey => _showHotkey;
    public bool ShowWindowRegistered => _showOk;
    public bool LauncherRegistered { get; private set; }

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
        // 不加 MOD_NOREPEAT：部分环境下会导致注册失败（沿用历史行为）
        _showOk = RegisterHotKey(_hwnd, IdShowWindow, mods, vk);
        if (_showOk)
            _showHotkey = hotkey;
        return _showOk;
    }

    /// <summary>Alt+Space 唤起快捷应用。注册失败说明组合键已被其它程序占用。</summary>
    public bool RegisterLauncherAltSpace()
    {
        Unregister(IdLauncher);
        LauncherRegistered = false;
        if (_hwnd == IntPtr.Zero)
            return false;
        LauncherRegistered = RegisterHotKey(_hwnd, IdLauncher, ModAlt, VkSpace);
        return LauncherRegistered;
    }

    /// <summary>挂到 <c>HwndSource.AddHook</c>；处理掉 WM_HOTKEY 并返回 true。</summary>
    public bool HandleMessage(int msg, IntPtr wParam)
    {
        if (msg != WmHotkey)
            return false;
        var id = wParam.ToInt32();
        if (id == IdShowWindow)
        {
            ShowWindowPressed?.Invoke();
            return true;
        }
        if (id == IdLauncher)
        {
            LauncherPressed?.Invoke();
            return true;
        }
        return false;
    }

    public void Dispose() => UnregisterAll();

    private void UnregisterAll()
    {
        Unregister(IdShowWindow);
        Unregister(IdLauncher);
        _showOk = false;
        LauncherRegistered = false;
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