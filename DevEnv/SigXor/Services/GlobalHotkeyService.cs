using System.Runtime.InteropServices;

namespace DevEnv.Host.SigXor.Services;

/// <summary>全局低级键盘钩子：过滤注入事件，分发按下/抬起。</summary>
public sealed class GlobalHotkeyService : IDisposable
{
    public const int VK_SPACE = 0x20;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_OEM_3 = 0xC0;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint LLKHF_INJECTED = 0x10;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId;

    public Func<int, bool>? KeyDown;
    public Func<int, bool>? KeyUp;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    public GlobalHotkeyService() => _proc = HookCallback;

    public bool Install()
    {
        if (_hookId != IntPtr.Zero) return true;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        return _hookId != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && lParam != IntPtr.Zero)
        {
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            bool injected = (data.flags & LLKHF_INJECTED) != 0;

            if (!injected)
            {
                bool isDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

                if (isDown && KeyDown?.Invoke((int)data.vkCode) == true)
                    return (IntPtr)1;

                if (isUp && KeyUp?.Invoke((int)data.vkCode) == true)
                    return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
