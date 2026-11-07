using System.Runtime.InteropServices;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 向系统注入真实键盘事件（SendInput）。低级键盘钩子会把这类事件标记为注入事件，
/// 生产逻辑据此忽略自动化按键（<c>LLKHF_INJECTED</c>）；自检探针则利用同一段代码模拟按键。
/// </summary>
internal static class KeyboardInputSender
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    /// <summary>发送一次按键按下或抬起；返回是否成功。</summary>
    public static bool SendKey(int virtualKey, bool keyUp)
    {
        var input = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)virtualKey,
                    dwFlags = keyUp ? KeyEventKeyUp : 0,
                },
            },
        };

        // INPUT 的实际大小由联合体中最大的 MOUSEINPUT 决定（x64 下 40 字节）；
        // cbSize 传错时 SendInput 会直接返回 0，按键根本发不出去。
        return SendInput(1, [input], Marshal.SizeOf<INPUT>()) == 1;
    }

    /// <summary>完整按一下再抬起。</summary>
    public static bool Tap(int virtualKey) => SendKey(virtualKey, false) && SendKey(virtualKey, true);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
