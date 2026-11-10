using System.Runtime.InteropServices;
using System.Windows.Threading;
using DevEnv.Core.Input;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 左 Alt 拦截钩子：单独按一下左 Alt 时打开启动器，组合键原样放行。
///
/// - 用 <c>WH_KEYBOARD_LL</c> 低级键盘钩子，只对真实按键生效（忽略注入事件，
///   自动化工具模拟的按键不会弹启动器）；
/// - 手势判断在 <see cref="AltKeyGestureTracker"/> 里，这里只负责转发；
/// - **不吞任何按键**：左 Alt 的按下与抬起都交给系统，Alt+Tab / Alt+F4 / Alt+空格
///   等组合键完全不受影响，前台程序也不会因为缺少抬起而把 Alt 误判成一直按着；
/// - 钩子回调运行在安装线程（UI 线程）上，必须尽快返回，真正的打开动作经
///   Dispatcher 投递到消息队列，避免触发系统的 LowLevelHooksTimeout 而被摘钩。
/// </summary>
internal sealed class AltKeyLauncherHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint LlkhfInjected = 0x10;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly Dispatcher _dispatcher;
    private readonly LowLevelKeyboardProc _proc;
    private readonly AltKeyGestureTracker _tracker = new();
    private IntPtr _hookId;
    private bool _suspended;

    public AltKeyLauncherHook(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _proc = HookCallback;
    }

    /// <summary>识别到「单独按左 Alt」时触发（已切到 UI 线程）。</summary>
    public event Action? LauncherRequested;

    /// <summary>
    /// 触发前的可用性判断。返回 false 时不触发，让左 Alt 照常交给前台程序
    /// （例如本程序的模态设置框正开着，此刻弹启动器没有意义）。
    /// </summary>
    public Func<bool>? CanTrigger { get; set; }

    /// <summary>
    /// 是否处理注入的按键。默认 false：自动化工具 / 宏键盘模拟的按键不该弹启动器；
    /// 只有自检探针需要打开它，用 SendInput 模拟按键来验证拦截链路。
    /// </summary>
    public bool AllowInjectedInput { get; init; }

    public bool IsInstalled => _hookId != IntPtr.Zero;

    /// <summary>钩子收到的 Alt 相关抬起事件次数，供自检探针断言「抬起确实被看到」。</summary>
    public int ObservedAltKeyUps { get; private set; }

    /// <summary>录制快捷键期间暂停左 Alt 手势，结束录制后恢复。</summary>
    public void Suspend()
    {
        _suspended = true;
        _tracker.Reset();
    }

    public void Resume() => _suspended = false;

    /// <summary>安装钩子；重复调用无副作用。</summary>
    public bool Install()
    {
        if (_hookId != IntPtr.Zero)
            return true;

        _tracker.Reset();
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
        return _hookId != IntPtr.Zero;
    }

    /// <summary>卸载钩子并清空手势状态。</summary>
    public void Uninstall()
    {
        _tracker.Reset();
        if (_hookId == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    public void Dispose() => Uninstall();

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (_suspended)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        if (nCode >= 0 && lParam != IntPtr.Zero)
        {
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var injected = (data.flags & LlkhfInjected) != 0;
            if (!injected || AllowInjectedInput)
            {
                var message = wParam.ToInt32();
                if (message is WmKeyDown or WmSysKeyDown)
                {
                    _tracker.OnKeyDown((int)data.vkCode);
                }
                else if (message is WmKeyUp or WmSysKeyUp)
                {
                    int virtualKey = (int)data.vkCode;
                    if (AltKeyGestureTracker.IsAltKey(virtualKey))
                        ObservedAltKeyUps++;

                    var result = _tracker.OnKeyUp(virtualKey);
                    if (result.Trigger && (CanTrigger?.Invoke() ?? true))
                    {
                        // 按键照常交给系统（不吞），只是在这里唤起启动器
                        _dispatcher.BeginInvoke(new Action(() => LauncherRequested?.Invoke()), DispatcherPriority.Input);
                    }
                }
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
