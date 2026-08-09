using System;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Threading;

namespace ProgramBox.Utils
{
    /// <summary>
    /// 单实例：二次启动时激活已有窗口并置顶，而不是再开一个进程。
    /// </summary>
    public static class SingleInstanceService
    {
        private const string MutexName = @"Local\ProgramBox.DevBox.SingleInstance";
        private const string ActivateEventName = @"Local\ProgramBox.DevBox.Activate";

        private const uint ASFW_ANY = 0xffffffff;
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private static Mutex? _mutex;
        private static EventWaitHandle? _activateEvent;
        private static CancellationTokenSource? _cts;
        private static MainWindow? _mainWindow;

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(uint dwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        /// <summary>
        /// 尝试以主实例启动。若已有实例在运行，则通知其置顶并返回 false。
        /// </summary>
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
                // 当前进程刚由用户启动，拥有前台权限；放行主实例抢前台
                AllowSetForegroundWindow(ASFW_ANY);
                activate.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // 主实例尚未建好事件时忽略
            }
            catch (UnauthorizedAccessException)
            {
            }

            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        /// <summary>
        /// 主窗口就绪后启动监听：收到二次启动信号时居中显示并置顶。
        /// </summary>
        public static void StartActivateListener(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            if (_activateEvent == null)
                return;

            _cts = new CancellationTokenSource();
            var thread = new Thread(() => ListenLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "DevBox.SingleInstance"
            };
            thread.Start();
        }

        public static void Dispose()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }

            _cts?.Dispose();
            _cts = null;

            _activateEvent?.Dispose();
            _activateEvent = null;

            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                }

                _mutex.Dispose();
                _mutex = null;
            }

            _mainWindow = null;
        }

        private static void ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_activateEvent == null)
                        break;

                    if (_activateEvent.WaitOne(500))
                        Dispatcher.UIThread.Post(ActivateExistingWindow);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (AbandonedMutexException)
                {
                    break;
                }
            }
        }

        private static void ActivateExistingWindow()
        {
            if (_mainWindow == null)
                return;

            // 居中 + 显示，观感接近“新开了一个窗口”
            _mainWindow.ShowCentered();

            var hwnd = _mainWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            ForceForeground(hwnd);

            // 短暂置顶，确保盖过当前前台窗口
            _mainWindow.Topmost = true;
            _mainWindow.Topmost = false;
            _mainWindow.Activate();
        }

        private static void ForceForeground(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;

            if (IsIconic(hwnd))
                ShowWindow(hwnd, SW_RESTORE);
            else
                ShowWindow(hwnd, SW_SHOW);

            var foreground = GetForegroundWindow();
            var foreThread = GetWindowThreadProcessId(foreground, out _);
            var appThread = GetCurrentThreadId();

            if (foreThread != 0 && foreThread != appThread)
            {
                AttachThreadInput(appThread, foreThread, true);
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
                AttachThreadInput(appThread, foreThread, false);
            }
            else
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
        }
    }
}
