using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using DevEnv.Core.Input;
using DevEnv.Host.SigXor.Diagnostics;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// 左 Alt 拦截自检：真实安装键盘钩子，再用 SendInput 模拟按键，验证
/// 「单独按左 Alt 触发」「Alt+组合键放行」「右 Alt 不触发」「卸载后不再触发」四条链路。
///
/// 关键点是把 <c>AllowInjectedInput</c> 打开——生产环境忽略注入按键，
/// 否则探针自己发的模拟按键会被钩子无视，什么也验证不到。
/// 组合键用 Alt+F24：Windows 没有这个绑定，即使拦截失效也不会有副作用。
///
/// 用法：<c>DevEnv.exe --altkey-check %TEMP%\altkey.txt</c>
/// </summary>
public static class AltKeyLauncherProbe
{
    public const string Argument = "--altkey-check";

    private const int VkLeftAlt = AltKeyGestureTracker.VkLeftAlt;
    private const int VkRightAlt = AltKeyGestureTracker.VkRightAlt;
    private const int VkF24 = 0x87;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    public static int Run(string[] args)
    {
        var report = new StringBuilder();
        var failures = 0;
        var skipped = false;

        void Check(string name, bool ok, string? detail = null)
        {
            if (ok)
            {
                report.AppendLine($"OK    {name}");
            }
            else
            {
                failures++;
                report.AppendLine($"FAIL  {name}{(detail is null ? "" : ": " + detail)}");
            }
        }

        var hook = new AltKeyLauncherHook(Dispatcher.CurrentDispatcher) { AllowInjectedInput = true };
        var triggers = 0;
        hook.LauncherRequested += () => triggers++;
        hook.CanTrigger = () => true;

        try
        {
            Check("键盘钩子安装成功", hook.Install());

            // 1) 单独按一下左 Alt → 触发一次，按键照常交给系统（修饰键状态不会被卡住）
            TapKey(VkLeftAlt);
            Settle();
            Check("单独按左 Alt 键触发全局启动器", triggers == 1, $"触发 {triggers} 次");
            Check("钩子收到了左 Alt 抬起", hook.ObservedAltKeyUps == 1, $"收到 {hook.ObservedAltKeyUps} 次");
            Check("左 Alt 键状态已复位（不会卡住修饰键）",
                (GetAsyncKeyState(VkLeftAlt) & 0x8000) == 0,
                "GetAsyncKeyState 仍为按下");

            // 2) Alt+F24 → 不应触发
            triggers = 0;
            Key(VkLeftAlt, up: false);
            Pump();
            Key(VkF24, up: false);
            Key(VkF24, up: true);
            Key(VkLeftAlt, up: true);
            Settle();
            Check("Alt+其它键不触发全局启动器", triggers == 0, $"触发 {triggers} 次");
            Check("Alt+其它键后左 Alt 状态已复位",
                (GetAsyncKeyState(VkLeftAlt) & 0x8000) == 0,
                "GetAsyncKeyState 仍为按下");

            // 3) 组合键之后单独按左 Alt 仍能触发（手势状态已复位）
            triggers = 0;
            TapKey(VkLeftAlt);
            Settle();
            Check("组合键后单按左 Alt 仍可触发全局启动器", triggers == 1, $"触发 {triggers} 次");

            // 4) 右 Alt（AltGr / 语音键）永不触发
            triggers = 0;
            TapKey(VkRightAlt);
            Settle();
            Check("单独按右 Alt 不触发全局启动器", triggers == 0, $"触发 {triggers} 次");

            // 5) 卸载后不再拦截
            hook.Uninstall();
            Check("键盘钩子可正常卸载", !hook.IsInstalled);
            triggers = 0;
            TapKey(VkLeftAlt);
            Settle();
            Check("卸载后不再触发", triggers == 0, $"触发 {triggers} 次");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("错误码 5"))
        {
            // 锁屏、远程会话断开、前台是提权窗口时，SendInput 会被系统拒绝（ACCESS_DENIED）；
            // 这是环境限制而不是代码问题，记为跳过，不报失败。
            skipped = true;
            report.AppendLine("SKIP  桌面当前不接受模拟按键（锁屏 / 远程会话断开 / 前台为提权窗口），请解锁后在本地桌面重跑");
        }
        catch (Exception ex)
        {
            failures++;
            report.AppendLine($"FAIL  探针异常: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            hook.Dispose();
        }

        var text = report.ToString();
        ProbeOutput.Line(text);
        ProbeOutput.Line(skipped
            ? "已跳过（环境不支持注入按键）"
            : $"失败 {failures} 项 / 共 {failures + text.Split('\n').Count(l => l.StartsWith("OK ", StringComparison.Ordinal))} 项");

        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            try { File.WriteAllText(args[0], ProbeOutput.Flush()); } catch { /* 报告写失败不影响退出码 */ }
        }

        return failures == 0 || skipped ? 0 : 1;
    }

    private static void TapKey(int virtualKey)
    {
        Key(virtualKey, up: false);
        Key(virtualKey, up: true);
    }

    /// <summary>等注入按键被系统处理完，再跑一轮消息循环。</summary>
    private static void Settle()
    {
        Thread.Sleep(120);
        Pump();
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    /// <summary>跑一轮消息循环，让钩子里投递到 Dispatcher 的回调执行完。</summary>
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Key(int virtualKey, bool up)
    {
        var input = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)virtualKey,
                    dwFlags = up ? KeyEventKeyUp : 0,
                },
            },
        };
        // INPUT 的实际大小由联合体中最大的 MOUSEINPUT 决定（x64 下 40 字节）。
        // cbSize 传错时 SendInput 会直接返回 0，按键根本发不出去。
        var sent = SendInput(1, [input], Marshal.SizeOf<INPUT>());
        if (sent != 1)
            throw new InvalidOperationException(
                $"SendInput 失败（错误码 {Marshal.GetLastWin32Error()}）");
    }

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
