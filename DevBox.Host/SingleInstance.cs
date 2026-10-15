using System.Runtime.InteropServices;

namespace DevBox.Host;

/// <summary>WinForms 单实例：二次启动时唤醒已有窗口。</summary>
internal static class SingleInstance
{
    private const string MutexName = @"Local\ProgramBox.DevBox.SingleInstance";
    private const string ActivateEventName = @"Local\ProgramBox.DevBox.Activate";

    private static Mutex? _mutex;
    private static EventWaitHandle? _activateEvent;
    private static CancellationTokenSource? _cts;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    public static bool TryStartAsPrimary()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            return true;
        }

        try
        {
            using var activate = EventWaitHandle.OpenExisting(ActivateEventName);
            AllowSetForegroundWindow(0xffffffff);
            activate.Set();
        }
        catch
        {
            // ignore
        }

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public static void StartActivateListener(Form form)
    {
        if (_activateEvent is null)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_activateEvent.WaitOne(500))
                    {
                        form.BeginInvoke(() =>
                        {
                            form.Show();
                            form.ShowInTaskbar = true;
                            form.WindowState = FormWindowState.Normal;
                            form.Activate();
                            form.BringToFront();
                        });
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }, token);
    }

    public static void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _cts?.Dispose(); } catch { /* ignore */ }
        try { _activateEvent?.Dispose(); } catch { /* ignore */ }
        try
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch
        {
            // ignore
        }

        _cts = null;
        _activateEvent = null;
        _mutex = null;
    }
}
