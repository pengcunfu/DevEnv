using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using DevEnv.Core.Input;
using DevEnv.Host.SigXor.Diagnostics;

namespace DevEnv.Host.Infrastructure;

/// <summary>
/// Win 键拦截自检：真实安装键盘钩子，再用 SendInput 模拟按键，验证
/// 「单独按 Win 触发」「Win+组合键放行」「卸载后不再拦截」三条链路。
///
/// 关键点是把 <c>AllowInjectedInput</c> 打开——生产环境忽略注入按键，
/// 否则探针自己发的模拟按键会被钩子无视，什么也验证不到。
/// 组合键用 Win+F24：Windows 没有这个绑定，即使拦截失效也不会有副作用。
///
/// 用法：<c>DevEnv.exe --winkey-check %TEMP%\winkey.txt</c>
/// </summary>
public static class WinKeyLauncherProbe
{
    public const string Argument = "--winkey-check";

    private const int VkLeftWin = WinKeyGestureTracker.VkLeftWin;
    private const int VkF24 = 0x87;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    public static int Run(string[] args)
    {
        var report = new StringBuilder();
        var failures = 0;

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

        var hook = new WinKeyLauncherHook(Dispatcher.CurrentDispatcher) { AllowInjectedInput = true };
        var triggers = 0;
        hook.LauncherRequested += () => triggers++;
        hook.CanTrigger = () => true;

        try
        {
            Check("键盘钩子安装成功", hook.Install());

            // 1) 单独按一下 Win → 触发一次，且这次抬起被吞掉（开始菜单不弹）
            TapKey(VkLeftWin);
            Pump();
            Check("单独按 Win 键触发启动台", triggers == 1, $"触发 {triggers} 次");
            Check("Win 键抬起被吞掉（开始菜单不会弹出）", hook.SwallowedKeyUps == 1, $"吞键 {hook.SwallowedKeyUps} 次");

            // 2) Win+F24 → 不应触发，抬起也应放行
            triggers = 0;
            var swallowedBefore = hook.SwallowedKeyUps;
            Key(VkLeftWin, up: false);
            Pump();
            Key(VkF24, up: false);
            Key(VkF24, up: true);
            Key(VkLeftWin, up: true);
            Pump();
            Check("Win+其它键不触发启动台", triggers == 0, $"触发 {triggers} 次");
            Check("Win+其它键的抬起原样放行", hook.SwallowedKeyUps == swallowedBefore, $"吞键 {hook.SwallowedKeyUps} 次");

            // 3) 组合键之后单独按 Win 仍能触发（手势状态已复位）
            triggers = 0;
            TapKey(VkLeftWin);
            Pump();
            Check("组合键后单按 Win 仍可触发", triggers == 1, $"触发 {triggers} 次");

            // 4) 卸载后不再拦截
            hook.Uninstall();
            Check("键盘钩子可正常卸载", !hook.IsInstalled);
            triggers = 0;
            TapKey(VkLeftWin);
            Pump();
            Check("卸载后不再触发", triggers == 0, $"触发 {triggers} 次");
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
        ProbeOutput.Line($"失败 {failures} 项 / 共 {failures + text.Split('\n').Count(l => l.StartsWith("OK ", StringComparison.Ordinal))} 项");

        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            try { File.WriteAllText(args[0], ProbeOutput.Flush()); } catch { /* 报告写失败不影响退出码 */ }
        }

        return failures == 0 ? 0 : 1;
    }

    private static void TapKey(int virtualKey)
    {
        Key(virtualKey, up: false);
        Key(virtualKey, up: true);
    }

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
