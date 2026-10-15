using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Win32;

namespace ClipboardApp.Services;

/// <summary>
/// 全局热键（Win32 RegisterHotKey），挂在 DevBox 主窗 HWND 上。
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x4342; // "CB"
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private readonly Window _window;
    private IntPtr _hwnd;
    private bool _registered;
    private bool _hooked;

    public event Action? HotkeyPressed;

    public HotkeyService(Window window)
    {
        _window = window;
        if (_window.IsLoaded || _window.IsVisible)
            EnsureHook();
        else
            _window.Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e) => EnsureHook();

    private void EnsureHook()
    {
        if (_hooked)
            return;

        var handle = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return;

        _hwnd = handle;
        Win32Properties.AddWndProcHookCallback(_window, WndProc);
        _hooked = true;
    }

    public bool Register(IEnumerable<string> modifiers, string key)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        EnsureHook();
        if (_hwnd == IntPtr.Zero)
            return false;

        Unregister();

        uint mod = 0;
        foreach (var modifier in modifiers)
        {
            mod |= modifier.ToLowerInvariant() switch
            {
                "ctrl" or "control" => ModControl,
                "alt" => ModAlt,
                "shift" => ModShift,
                "win" or "meta" or "windows" => ModWin,
                _ => 0u,
            };
        }

        var vk = KeyToVirtualKey(key);
        if (vk == 0)
            return false;

        _registered = RegisterHotKey(_hwnd, HotkeyId, mod, vk);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Dispatcher.UIThread.Post(() => HotkeyPressed?.Invoke());
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _window.Opened -= OnOpened;
        Unregister();
        if (_hooked)
        {
            Win32Properties.RemoveWndProcHookCallback(_window, WndProc);
            _hooked = false;
        }
    }

    public static string FormatHotkey(IEnumerable<string> modifiers, string key)
        => string.Join('+', modifiers.Select(m => m.ToUpperInvariant()).Append(key.ToUpperInvariant()));

    private static uint KeyToVirtualKey(string key)
    {
        var k = key.Trim().ToLowerInvariant();
        return k switch
        {
            "space" => 0x20,
            "esc" or "escape" => 0x1B,
            "f1" => 0x70, "f2" => 0x71, "f3" => 0x72, "f4" => 0x73,
            "f5" => 0x74, "f6" => 0x75, "f7" => 0x76, "f8" => 0x77,
            "f9" => 0x78, "f10" => 0x79, "f11" => 0x7A, "f12" => 0x7B,
            _ when k.Length == 1 && k[0] is >= 'a' and <= 'z' => (uint)(k[0] - 'a' + 0x41),
            _ when k.Length == 1 && k[0] is >= '0' and <= '9' => (uint)k[0],
            _ => 0,
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
