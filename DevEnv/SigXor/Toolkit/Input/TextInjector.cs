using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DevEnv.Host.SigXor.Toolkit.Input;

public sealed class TextInjector
{
    public void CaptureTargetWindow() => TargetWindowTracker.Capture();

    public void RestoreTargetWindow() => TargetWindowTracker.Restore();

    public async Task InsertTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        string? original = Win32Clipboard.GetText();
        try
        {
            Win32Clipboard.SetText(text);
            TargetWindowTracker.Restore();
            await Task.Delay(120);
            Win32TextInput.Paste();
            await Task.Delay(100);
        }
        finally
        {
            try
            {
                if (!string.IsNullOrEmpty(original))
                    Win32Clipboard.SetText(original);
                else
                    Win32Clipboard.Clear();
            }
            catch
            {
            }
        }
    }

    public async Task TypeTextAsync(string text, double typingDelaySeconds)
    {
        if (string.IsNullOrEmpty(text)) return;

        TargetWindowTracker.Restore();
        await Task.Delay(120);

        double delay = Math.Clamp(typingDelaySeconds, 0, 2);
        foreach (char ch in text)
        {
            if (ch == '\r') continue;
            if (ch == '\n')
                Win32TextInput.SendEnter();
            else if (ch == '\t')
                Win32TextInput.SendTab();
            else
                Win32TextInput.SendChar(ch);

            if (delay > 0)
                await Task.Delay(TimeSpan.FromSeconds(delay));
        }
    }
}

internal static class TargetWindowTracker
{
    private static IntPtr _targetWindow;
    private static readonly uint OwnProcessId = (uint)Process.GetCurrentProcess().Id;
    private const int SwRestore = 9;

    public static void Capture()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == OwnProcessId) return;
        _targetWindow = hwnd;
    }

    public static void Restore()
    {
        if (_targetWindow == IntPtr.Zero) return;

        if (GetForegroundWindow() == _targetWindow)
        {
            FocusTargetControl();
            return;
        }

        IntPtr foreground = GetForegroundWindow();
        uint foregroundThread = foreground != IntPtr.Zero
            ? GetWindowThreadProcessId(foreground, out _)
            : 0;
        uint targetThread = GetWindowThreadProcessId(_targetWindow, out _);
        uint currentThread = GetCurrentThreadId();

        if (foregroundThread != 0 && foregroundThread != targetThread)
            AttachThreadInput(foregroundThread, targetThread, true);
        if (currentThread != targetThread)
            AttachThreadInput(currentThread, targetThread, true);

        if (IsIconic(_targetWindow))
            ShowWindow(_targetWindow, SwRestore);

        BringWindowToTop(_targetWindow);
        SetForegroundWindow(_targetWindow);
        FocusTargetControl();

        if (currentThread != targetThread)
            AttachThreadInput(currentThread, targetThread, false);
        if (foregroundThread != 0 && foregroundThread != targetThread)
            AttachThreadInput(foregroundThread, targetThread, false);
    }

    private static void FocusTargetControl()
    {
        uint targetThread = GetWindowThreadProcessId(_targetWindow, out _);
        uint currentThread = GetCurrentThreadId();
        bool attached = false;
        if (currentThread != targetThread)
            attached = AttachThreadInput(currentThread, targetThread, true);

        var info = new GuiThreadInfo { cbSize = Marshal.SizeOf<GuiThreadInfo>() };
        if (GetGUIThreadInfo(targetThread, ref info) && info.hwndFocus != IntPtr.Zero)
            SetFocus(info.hwndFocus);

        if (attached)
            AttachThreadInput(currentThread, targetThread, false);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public Rect rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo info);
}

internal static class Win32Clipboard
{
    private const uint CfUnicode = 13;
    private const uint GmemMoveable = 0x0002;

    public static string? GetText()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            IntPtr handle = GetClipboardData(CfUnicode);
            if (handle == IntPtr.Zero) return null;

            IntPtr pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(pointer); }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    public static void SetText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
            throw new InvalidOperationException("无法打开剪贴板");

        try
        {
            EmptyClipboard();
            byte[] bytes = Encoding.Unicode.GetBytes(text + '\0');
            IntPtr hGlobal = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
            if (hGlobal == IntPtr.Zero)
                throw new InvalidOperationException("无法分配剪贴板内存");

            IntPtr target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                throw new InvalidOperationException("无法锁定剪贴板内存");
            }

            try { Marshal.Copy(bytes, 0, target, bytes.Length); }
            finally { GlobalUnlock(hGlobal); }

            if (SetClipboardData(CfUnicode, hGlobal) == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                throw new InvalidOperationException("无法写入剪贴板");
            }
        }
        finally { CloseClipboard(); }
    }

    public static void Clear()
    {
        if (!OpenClipboard(IntPtr.Zero)) return;
        try { EmptyClipboard(); }
        finally { CloseClipboard(); }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}

internal static class Win32TextInput
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private static readonly int InputSize = Marshal.SizeOf<Input>();
    private const ushort VkReturn = 0x0D;
    private const ushort VkTab = 0x09;

    public static void SendChar(char ch)
    {
        var inputs = new Input[2];
        inputs[0].type = InputKeyboard;
        inputs[0].Union.ki = new KeyboardInput { wScan = ch, dwFlags = KeyEventFUnicode };
        inputs[1].type = InputKeyboard;
        inputs[1].Union.ki = new KeyboardInput
        {
            wScan = ch,
            dwFlags = KeyEventFUnicode | KeyEventFKeyUp,
        };
        SendInput(2, inputs, InputSize);
    }

    public static void SendEnter() => SendKey(VkReturn);
    public static void SendTab() => SendKey(VkTab);

    public static void Paste() => SendKeyCombo(0x11, 0x56);

    private static void SendKey(ushort vk)
    {
        var inputs = new Input[2];
        inputs[0] = KeyDown(vk);
        inputs[1] = KeyUp(vk);
        SendInput(2, inputs, InputSize);
    }

    private static void SendKeyCombo(ushort modifier, ushort key)
    {
        var inputs = new Input[4];
        inputs[0] = KeyDown(modifier);
        inputs[1] = KeyDown(key);
        inputs[2] = KeyUp(key);
        inputs[3] = KeyUp(modifier);
        SendInput(4, inputs, InputSize);
    }

    private static Input KeyDown(ushort vk) => new()
    {
        type = InputKeyboard,
        Union = new InputUnion { ki = new KeyboardInput { wVk = vk } },
    };

    private static Input KeyUp(ushort vk) => new()
    {
        type = InputKeyboard,
        Union = new InputUnion { ki = new KeyboardInput { wVk = vk, dwFlags = KeyEventFKeyUp } },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput mi;
        [FieldOffset(0)] public KeyboardInput ki;
        [FieldOffset(0)] public HardwareInput hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint type;
        public InputUnion Union;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] inputs, int cbSize);
}
