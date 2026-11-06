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

    /// <summary>
    /// 未分配的虚拟键（AHK 里常用的「假按键」）。单独按 Win 时用它打断
    /// 「单击 Win」判定，开始菜单就不会弹出。
    /// </summary>
    private const int VkDummy = 0xE8;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventExtendedKey = 0x0001;

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
                    var virtualKey = (int)data.vkCode;
                    var result = _tracker.OnKeyUp(virtualKey);
                    // 不可用时（模态框开着）不吞键，让系统开始菜单照常弹出
                    if (result.Trigger && (CanTrigger?.Invoke() ?? true))
                    {
                        // 吞掉物理抬起后，系统仍认为 Win 键按着；若不管它，之后按任何键
                        // 都会被当成 Win+组合键（这就是「Win 接管后按键异常」的根因）。
                        // 这里补发「假按键 + Win 抬起」：假按键打断开始菜单的单击判定，
                        // Win 抬起让系统的按键状态复位。
                        ReleaseWinKey(virtualKey);
                        _dispatcher.BeginInvoke(new Action(() => LauncherRequested?.Invoke()), DispatcherPriority.Input);
                        SwallowedKeyUps++;
                        return new IntPtr(1);
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>
    /// 吞掉物理 Win 抬起后补发按键：先送一个未分配的假按键（打断开始菜单的单击判定），
    /// 再送 Win 抬起（让系统的按键状态复位，后续按键不再被当成 Win+组合键）。
    /// </summary>
    private static void ReleaseWinKey(int virtualKey)
    {
        // 右 Win 是扩展键，补发时带上 EXTENDEDKEY 才和真实抬起等价
        var flags = virtualKey == WinKeyGestureTracker.VkRightWin ? KeyEventExtendedKey : 0;
        Send(VkDummy, 0);
        Send(VkDummy, KeyEventKeyUp);
        Send(virtualKey, flags | KeyEventKeyUp);
    }

    private static void Send(int virtualKey, uint flags)
    {
        var input = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = (ushort)virtualKey, dwFlags = flags },
            },
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    /// <summary>联合体的实际大小由最大的 MOUSEINPUT 决定，cbSize 传小会直接失败。</summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
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
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

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
