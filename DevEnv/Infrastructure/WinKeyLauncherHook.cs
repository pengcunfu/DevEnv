using System.Runtime.InteropServices;
using System.Windows.Threading;
using DevEnv.Core.Input;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// Win 键拦截钩子：单独按一下 Win 键时打开快捷方式启动台，组合键原样放行。
///
/// - 用 <c>WH_KEYBOARD_LL</c> 低级键盘钩子，只对真实按键生效（忽略注入事件，
///   自动化工具模拟的 Win 键不会弹启动台）；
/// - 手势判断在 <see cref="WinKeyGestureTracker"/> 里，这里只负责转发与吞键；
/// - 钩子回调运行在安装线程（UI 线程）上，必须尽快返回，真正的打开动作经
///   Dispatcher 投递到消息队列，避免触发系统的 LowLevelHooksTimeout 而被摘钩。
/// </summary>
internal sealed class WinKeyLauncherHook : IDisposable
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
    private readonly WinKeyGestureTracker _tracker = new();
    private IntPtr _hookId;

    public WinKeyLauncherHook(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _proc = HookCallback;
    }

    /// <summary>识别到「单独按 Win」时触发（已切到 UI 线程）。</summary>
    public event Action? LauncherRequested;

    /// <summary>
    /// 触发前的可用性判断。返回 false 时既不吞键也不触发，让 Win 键照常打开系统开始菜单
    /// （例如本程序的模态设置框正开着，此刻弹启动台没有意义）。
    /// </summary>
    public Func<bool>? CanTrigger { get; set; }

    /// <summary>
    /// 是否处理注入的按键。默认 false：自动化工具 / 宏键盘模拟的 Win 键不该弹启动台；
    /// 只有自检探针需要打开它，用 SendInput 模拟按键来验证拦截链路。
    /// </summary>
    public bool AllowInjectedInput { get; init; }

    public bool IsInstalled => _hookId != IntPtr.Zero;

    /// <summary>累计吞掉的 Win 键抬起次数（开始菜单就是靠吞这一次抬起挡住的），供自检探针断言。</summary>
    public int SwallowedKeyUps { get; private set; }

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
                    var result = _tracker.OnKeyUp((int)data.vkCode);
                    // 不可用时（模态框开着）不吞键，让系统开始菜单照常弹出
                    if (result.Trigger && (CanTrigger?.Invoke() ?? true))
                    {
                        _dispatcher.BeginInvoke(new Action(() => LauncherRequested?.Invoke()), DispatcherPriority.Input);
                        SwallowedKeyUps++;
                        // 吞掉这次抬起：开始菜单正是在 Win 抬起时弹出的
                        return new IntPtr(1);
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
