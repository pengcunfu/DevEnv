using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace DevEnv.Host.Infrastructure;

/// <summary>单实例：二次启动时通过命名事件唤醒已有主窗（WPF 下由 Dispatcher 回到 UI 线程）。</summary>
internal static class SingleInstance
{
    private const string MutexName = @"Local\FNSoftware.DevEnv.SingleInstance";
    private const string ActivateEventName = @"Local\FNSoftware.DevEnv.Activate";

    /// <summary>
    /// 文件参数转发用的命名管道名。
    /// 二次启动时把要预览的文件路径写进管道，主实例读到后弹预览浮窗
    /// （「用FireNeb DevEnv打开文件」与资源管理器里的文件关联都靠它）。
    /// </summary>
    private const string FilePipeName = @"Local\FNSoftware.DevEnv.FilePipe";

    private static Mutex? _mutex;
    private static EventWaitHandle? _activateEvent;
    private static CancellationTokenSource? _cts;
    private static CancellationTokenSource? _fileCts;

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

    /// <summary>
    /// 二次启动时把要打开的文件路径转给主实例。
    /// 返回 true 表示已转发（调用方应退出自身）。
    /// </summary>
    public static bool ForwardFilesToPrimary(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
            return false;

        try
        {
            using var pipe = new NamedPipeClientStream(".", FilePipeName, PipeDirection.Out);
            pipe.Connect(2000);

            // 先写长度再写路径，避免路径里的换行被误当分隔符
            string payload = string.Join('\n', paths);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(payload);
            pipe.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();

            AllowSetForegroundWindow(0xffffffff);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 后台监听文件转发管道，收到路径后经 <paramref name="dispatcher"/> 回到 UI 线程执行。
    /// </summary>
    public static void StartFileListener(Dispatcher dispatcher, Action<string[]> onFiles)
    {
        _fileCts = new CancellationTokenSource();
        var token = _fileCts.Token;

        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        FilePipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    pipe.WaitForConnectionAsync(token).GetAwaiter().GetResult();

                    byte[] lenBytes = new byte[4];
                    if (pipe.Read(lenBytes, 0, 4) == 4)
                    {
                        int length = BitConverter.ToInt32(lenBytes, 0);
                        if (length > 0 && length <= 1024 * 1024)
                        {
                            byte[] payload = new byte[length];
                            int read = 0;
                            while (read < length)
                            {
                                int n = pipe.Read(payload, read, length - read);
                                if (n <= 0) break;
                                read += n;
                            }

                            if (read == length)
                            {
                                string text = System.Text.Encoding.UTF8.GetString(payload);
                                string[] paths = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                                if (paths.Length > 0)
                                    dispatcher.InvokeAsync(() => onFiles(paths), DispatcherPriority.Normal);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 管道被占用 / 断开：等下一轮
                    try { Task.Delay(200, token).GetAwaiter().GetResult(); } catch { break; }
                }
            }
        }, token);
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
        try { _fileCts?.Cancel(); } catch { /* ignore */ }
        try { _fileCts?.Dispose(); } catch { /* ignore */ }
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
        _fileCts = null;
        _activateEvent = null;
        _mutex = null;
    }
}