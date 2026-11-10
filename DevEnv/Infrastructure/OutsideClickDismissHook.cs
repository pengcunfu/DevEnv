using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 启动器显示期间的窗口外点击监听：鼠标按在启动器窗口矩形之外时收起窗口。
///
/// 失焦收起已经由 <c>Window.Deactivated</c> 负责，但有几种点击不会触发它
/// （点击任务栏空白处、窗口还没真正拿到前台权限、被系统弹出层挡住等），
/// 这里补一层明确的窗口外点击检测，保证「点外面就收起」始终成立。
///
/// - 只监听、不拦截：点击照常交给目标程序；
/// - 只在启动器可见期间安装，隐藏后立即卸载；
/// - 忽略注入点击，与左 Alt / Win 键钩子保持一致；
/// - 钩子回调在安装它的 UI 线程上执行，运行状态判定可以直接读窗口字段。
/// </summary>
internal sealed class OutsideClickDismissHook : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmRightButtonDown = 0x0204;
    private const int WmMiddleButtonDown = 0x0207;
    private const uint LlmhfInjected = 0x01;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly Dispatcher _dispatcher;
    private readonly Func<IntPtr> _windowHandle;
    private readonly Func<bool> _isSuppressed;
    private readonly LowLevelMouseProc _proc;
    private IntPtr _hookId;

    /// <param name="dispatcher">投递收起动作的 UI 线程。</param>
    /// <param name="windowHandle">启动器窗口句柄，用来取屏幕矩形。</param>
    /// <param name="isSuppressed">此刻是否应忽略点击（菜单 / 模态对话框 / 框选 / 拖拽 / 正在收起）。</param>
    public OutsideClickDismissHook(
        Dispatcher dispatcher,
        Func<IntPtr> windowHandle,
        Func<bool> isSuppressed)
    {
        _dispatcher = dispatcher;
        _windowHandle = windowHandle;
        _isSuppressed = isSuppressed;
        _proc = HookCallback;
    }

    /// <summary>收到落在启动器窗口之外的鼠标按下时触发（已切到 UI 线程）。</summary>
    public event Action? OutsideClicked;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    /// <summary>安装钩子；重复调用无副作用。</summary>
    public bool Install()
    {
        if (_hookId != IntPtr.Zero)
            return true;

        _hookId = SetWindowsHookEx(WhMouseLl, _proc, GetModuleHandle(null), 0);
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
        if (nCode >= 0 && lParam != IntPtr.Zero && !_isSuppressed())
        {
            var message = wParam.ToInt32();
            if (message is WmLeftButtonDown or WmRightButtonDown or WmMiddleButtonDown)
            {
                var data = Marshal.PtrToStructure<MsLlHookStruct>(lParam);
                if ((data.flags & LlmhfInjected) == 0
                    && !IsPointInsideWindow(data.pt.X, data.pt.Y))
                {
                    _dispatcher.BeginInvoke(new Action(() => OutsideClicked?.Invoke()), DispatcherPriority.Input);
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>钩子回调必须尽快返回，这里只做一次 GetWindowRect；取不到边界时保守地当成窗口内。</summary>
    private bool IsPointInsideWindow(int x, int y)
    {
        var handle = _windowHandle();
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect))
            return true;

        return x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsLlHookStruct
    {
        public Point pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out WindowRect lpRect);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
