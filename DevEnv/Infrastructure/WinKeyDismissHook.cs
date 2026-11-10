using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 启动器显示期间的 Win 键监听：用户按 Win 想打开开始菜单时，先把启动器收起，
/// 否则铺满全屏且置顶的启动器会把开始菜单压在下面，看起来像 Win 键没反应。
///
/// - 只监听、不拦截：Win 键照常交给系统打开开始菜单，我们只是在自己的窗口上收起；
/// - 只在启动器可见期间安装，隐藏后立即卸载，不常驻；
/// - 忽略注入按键，与左 Alt 钩子保持一致。
/// </summary>
internal sealed class WinKeyDismissHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint LlkhfInjected = 0x10;
    private const int VkLeftWin = 0x5B;
    private const int VkRightWin = 0x5C;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly Dispatcher _dispatcher;
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId;

    public WinKeyDismissHook(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _proc = HookCallback;
    }

    /// <summary>收到真实的 Win 键按下时触发（已切到 UI 线程）。</summary>
    public event Action? WindowsKeyPressed;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    /// <summary>安装钩子；重复调用无副作用。</summary>
    public bool Install()
    {
        if (_hookId != IntPtr.Zero)
            return true;

        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
        return _hookId != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    public void Dispose() => Uninstall();

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && lParam != IntPtr.Zero && wParam.ToInt32() is WmKeyDown or WmSysKeyDown)
        {
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if ((data.flags & LlkhfInjected) == 0 && data.vkCode is VkLeftWin or VkRightWin)
            {
                // 钩子回调必须尽快返回；收起动作投递到 UI 线程，Win 键本身照常放行
                _dispatcher.BeginInvoke(new Action(() => WindowsKeyPressed?.Invoke()), DispatcherPriority.Input);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

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
