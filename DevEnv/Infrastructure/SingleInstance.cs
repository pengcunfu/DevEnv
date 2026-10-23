using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace DevEnv.Host.Infrastructure;

/// <summary>单实例：二次启动时通过命名事件唤醒已有主窗（WPF 下由 Dispatcher 回到 UI 线程）。</summary>
internal static class SingleInstance
{
    private const string MutexName = @"Local\FNSoftware.DevEnv.SingleInstance";
    private const string ActivateEventName = @"Local\FNSoftware.DevEnv.Activate";

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

    /// <summary>后台等待唤醒事件，命中后经 <paramref name="dispatcher"/> 回到 UI 线程执行。</summary>
    public static void StartActivateListener(Dispatcher dispatcher, Action activate)
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
                    if (!_activateEvent.WaitOne(500))
                        continue;
                    if (token.IsCancellationRequested)
                        break;
                    dispatcher.InvokeAsync(activate, DispatcherPriority.Normal);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (OperationCanceledException)
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