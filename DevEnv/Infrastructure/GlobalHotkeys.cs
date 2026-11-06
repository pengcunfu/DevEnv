using System.Runtime.InteropServices;
using DevEnv.Core.Input;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 全局热键：显示窗口 / 快捷应用。组合键来自 data.json，可在「设置 → 快捷键」修改。
/// 纯 P/Invoke，与 UI 框架无关；WPF 下由 <c>HwndSource.AddHook</c> 接管 <c>WM_HOTKEY</c>。
/// </summary>
internal sealed class GlobalHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int IdShowWindow = 0xD0B0;
    private const int IdLauncher = 0x4C41;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _hwnd;
    private string _showHotkey = "";
    private string _launcherHotkey = "";
    private bool _showOk;
    private bool _suspended;

    public event Action? ShowWindowPressed;
    public event Action? LauncherPressed;

    public GlobalHotkeys(IntPtr hwnd) => _hwnd = hwnd;

    public string ShowHotkey => _showHotkey;
    public string LauncherHotkey => _launcherHotkey;
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

    /// <summary>全局唤起快捷应用。注册失败说明组合键已被其它程序占用或格式非法。</summary>
    public bool ApplyLauncherHotkey(string? hotkey)
    {
        Unregister(IdLauncher);
        _launcherHotkey = "";
        LauncherRegistered = false;
        if (_hwnd == IntPtr.Zero || _suspended || string.IsNullOrWhiteSpace(hotkey))
            return false;
        if (!HotkeyParser.TryParse(hotkey, out HotkeyGesture gesture)
            || !HotkeyValidator.Validate(gesture).IsValid)
        {
            return false;
        }

        LauncherRegistered = RegisterHotKey(_hwnd, IdLauncher, (uint)gesture.Modifiers, (uint)gesture.VirtualKey);
        if (LauncherRegistered)
            _launcherHotkey = HotkeyParser.FormatCanonical(gesture);
        return LauncherRegistered;
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
}
