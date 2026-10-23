using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DevEnv.Core;
using DevEnv.Core.Services;
using DevEnv.Core.LavaEnv.Services;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.Themes;
using DevEnv.Host.Windows;
using Velopack;
using Forms = System.Windows.Forms;
using WpfClipboard = System.Windows.Clipboard;

namespace DevEnv.Host;

/// <summary>
/// 应用入口：单实例、托盘、全局热键、剪贴板轮询都在这里装配。
/// 关闭主窗只是隐藏（进托盘），只有显式退出或 Velopack 更新才真正 Shutdown。
/// </summary>
public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private TrayIconHost? _tray;
    private GlobalHotkeys? _hotkeys;
    private HwndSourceHook? _hotkeyHook;
    private ClipboardWatcher? _clipboardWatcher;
    private TranslatorWindow? _translatorWindow;
    private ClipboardWindow? _clipboardWindow;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        // Velopack 必须最先处理：安装 / 卸载 / 更新重启都在参数里
        VelopackApp.Build().SetArgs(e.Args).Run();

        // 右键菜单管理提权子进程：执行完立即退出（绕过单实例）
        var applyIndex = Array.FindIndex(e.Args, a =>
            string.Equals(a, ContextMenuService.ElevatedArgument, StringComparison.OrdinalIgnoreCase));
        if (applyIndex >= 0 && applyIndex + 1 < e.Args.Length)
        {
            ContextMenuService.ExecuteElevatedRequest(e.Args[applyIndex + 1]);
            Shutdown();
            return;
        }

        // 管理员编辑 Hosts：绕过单实例，独立小窗
        if (e.Args.Any(a => string.Equals(a, ElevationHelper.EditHostsArgument, StringComparison.OrdinalIgnoreCase)))
        {
            var hostsWindow = new HostsEditWindow();
            hostsWindow.Closed += (_, _) => Shutdown();
            hostsWindow.Show();
            return;
        }

        if (!SingleInstance.TryStartAsPrimary())
        {
            Shutdown();
            return;
        }

        CoreServices.Initialize();
        ThemeManager.ApplyFromSettings();
        WireHostCallbacks();

        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        _mainWindow.SourceInitialized += (_, _) => EnsureHotkeys(_mainWindow);
        _mainWindow.Show();

        SingleInstance.StartActivateListener(Dispatcher, ShowMainWindow);

        _tray = new TrayIconHost("程序盒子", LoadAppIcon());
        _tray.ShowWindowRequested += ShowMainWindow;
        _tray.TranslatorRequested += () => Dispatcher.InvokeAsync(() => ShowTranslatorWindow(new TranslatorRequest { Visible = true }));
        _tray.ClipboardRequested += () => Dispatcher.InvokeAsync(() => ShowClipboardWindow(true));
        _tray.ExitRequested += ExitApp;

        _clipboardWatcher = new ClipboardWatcher();
        _clipboardWatcher.Captured += OnClipboardCaptured;
    }

    #region 全局异常

    /// <summary>未处理异常落盘到 %LOCALAPPDATA%\DevEnv\crash.log：桌面程序崩了用户看不到控制台。</summary>
    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevEnv");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 记录失败就算了
        }

        e.Handled = true;
    }

    #endregion

    #region 主窗与托盘

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
            return;
        ToolWindowHost.RefreshAll();
        _mainWindow.Show();
        _mainWindow.ShowInTaskbar = true;
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    /// <summary>关闭主窗 → 进托盘而不是退出；返回 true 表示已隐藏（调用方应取消关闭）。</summary>
    internal bool TryHideToTray()
    {
        if (_exiting || _mainWindow is null)
            return false;
        _mainWindow.Hide();
        _mainWindow.ShowInTaskbar = false;
        if (_tray is not null && !_tray.Visible)
            _tray.Visible = true;
        return true;
    }

    private void ExitApp()
    {
        if (_exiting)
            return;
        _exiting = true;

        _clipboardWatcher?.Dispose();
        _clipboardWatcher = null;
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        if (_hotkeyHook is not null && _hotkeys is not null
            && HwndSource.FromHwnd(new WindowInteropHelper(_mainWindow!).Handle) is { } source)
        {
            source.RemoveHook(_hotkeyHook);
        }
        _hotkeyHook = null;
        _hotkeys?.Dispose();
        _hotkeys = null;

        ToolWindowHost.CloseAll();
        _translatorWindow?.PrepareForExit();
        _clipboardWindow?.PrepareForExit();
        try { _translatorWindow?.Close(); } catch { /* 已在退出路径 */ }
        try { _clipboardWindow?.Close(); } catch { /* 已在退出路径 */ }
        try { _mainWindow?.Close(); } catch { /* 已在退出路径 */ }
        Shutdown();
    }

    private static System.Drawing.Icon? LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");
        try
        {
            return File.Exists(path) ? new System.Drawing.Icon(path) : null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region 全局热键

    private void EnsureHotkeys(MainWindow window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        if (_hotkeys is null)
        {
            _hotkeys = new GlobalHotkeys(hwnd);
            _hotkeys.ShowWindowPressed += ShowMainWindow;
            _hotkeys.TranslatorPressed += () => Dispatcher.InvokeAsync(() => ShowTranslatorWindow(new TranslatorRequest
            {
                Visible = true,
                FillClipboard = true,
            }));
            _hotkeys.ClipboardPressed += () => Dispatcher.InvokeAsync(() => ShowClipboardWindow(true));
        }
        else
        {
            _hotkeys.Rebind(hwnd);
        }

        _hotkeyHook = (IntPtr _, int msg, IntPtr wParam, IntPtr _, ref bool handled) =>
        {
            if (_hotkeys!.HandleMessage(msg, wParam))
                handled = true;
            return IntPtr.Zero;
        };
        if (HwndSource.FromHwnd(hwnd) is { } source)
            source.AddHook(_hotkeyHook);

        try
        {
            var cfg = CoreServices.Settings.GetConfig();
            _hotkeys.ApplyShowWindowHotkey(string.IsNullOrWhiteSpace(cfg.ShowWindowHotkey) ? "Ctrl+Alt+D" : cfg.ShowWindowHotkey);
        }
        catch
        {
            _hotkeys.ApplyShowWindowHotkey("Ctrl+Alt+D");
        }

        var altOk = _hotkeys.RegisterTranslatorAltSpace();
        var ctrlOk = _hotkeys.RegisterClipboardCtrlSpace();
        if (altOk && ctrlOk)
            return;

        var failed = new List<string>();
        if (!altOk) failed.Add("Alt+Space（熔岩翻译）");
        if (!ctrlOk) failed.Add("Ctrl+Space（熔岩剪贴板）");
        // 常见原因：旧版程序或输入法已占用
        Dispatcher.BeginInvoke(new Action(() =>
            _tray?.ShowBalloon($"快捷键注册失败：{string.Join("、", failed)}。请关闭其它占用程序后重启。",
                Forms.ToolTipIcon.Warning)));
    }

    /// <summary>唤起 / 隐藏翻译窗（WPF 外壳 + WebView2）。</summary>
    internal void ShowTranslatorWindow(TranslatorRequest request)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _translatorWindow ??= CreateTranslatorWindow();
            if (!request.Visible)
            {
                _translatorWindow.HideAndRelease();
                return;
            }
            _translatorWindow.ShowAndActivate();
            _translatorWindow.Apply(request);
        }));
    }

    /// <summary>唤起 / 隐藏剪贴板窗（纯 WPF）。</summary>
    internal void ShowClipboardWindow(bool visible)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _clipboardWindow ??= CreateClipboardWindow();
            if (visible)
                _clipboardWindow.ShowAndActivate();
            else
                _clipboardWindow.Hide();
        }));
    }

    private TranslatorWindow CreateTranslatorWindow()
    {
        var window = new TranslatorWindow();
        if (_mainWindow is not null)
            window.Owner = _mainWindow;
        if (Dispatcher.CheckAccess() || _tray is null)
            return window;

        // 环境创建是异步的，共享 Environment 让浏览器进程复用
        _ = Dispatcher.InvokeAsync(async () =>
        {
            var env = await WebView.WebView2EnvironmentFactory.GetSharedAsync();
            window.AttachEnvironment(env);
        });
        return window;
    }

    private ClipboardWindow CreateClipboardWindow()
    {
        var window = new ClipboardWindow();
        if (_mainWindow is not null)
            window.Owner = _mainWindow;
        return window;
    }

    #endregion

    #region 宿主回调（核心层 → UI 的唯一接缝）

    private void WireHostCallbacks()
    {
        HostCallbacks.OnTrayIconChanged += visible =>
            Dispatcher.BeginInvoke(new Action(() => _tray!.Visible = visible));
        HostCallbacks.OnShowWindowHotkeyChanged += hotkey =>
            Dispatcher.BeginInvoke(new Action(() => _hotkeys?.ApplyShowWindowHotkey(hotkey)));
        HostCallbacks.OnExitApp += () => Dispatcher.BeginInvoke(ExitApp);
        HostCallbacks.OnBeforeExitForUpdate += () => _exiting = true;
        HostCallbacks.SetClipboardText += text =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { WpfClipboard.SetText(text); } catch { /* 被占用 */ }
            }));
        HostCallbacks.SetClipboardImagePath += path =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(path);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    WpfClipboard.SetImage(bitmap);
                }
                catch
                {
                    // ignore
                }
            }));
        HostCallbacks.RestartElevated += asAdmin => Dispatcher.BeginInvoke(new Action(() => RestartProcess(asAdmin)));
        HostCallbacks.PickExecutable = () =>
        {
            using var dlg = new Forms.OpenFileDialog { Filter = "可执行文件 (*.exe)|*.exe", Multiselect = false };
            return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.FileName : null;
        };
        HostCallbacks.PickFolder = () =>
        {
            using var dlg = new Forms.FolderBrowserDialog();
            return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.SelectedPath : null;
        };
        HostCallbacks.PickFiles = filter =>
        {
            using var dlg = new Forms.OpenFileDialog
            {
                Filter = string.IsNullOrWhiteSpace(filter) ? "所有文件|*.*" : filter,
                Multiselect = true,
            };
            return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.FileNames : null;
        };
        HostCallbacks.SaveFile = (suggested, filter) =>
        {
            using var dlg = new Forms.SaveFileDialog
            {
                FileName = suggested,
                Filter = string.IsNullOrWhiteSpace(filter) ? "所有文件|*.*" : filter,
            };
            return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.FileName : null;
        };
        HostCallbacks.OnActivateTab += tab => Dispatcher.BeginInvoke(new Action(() =>
        {
            // 系统工具目录里的内置工具（tools/json 等）改为开独立窗口
            var tool = tab?.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) == true
                ? tab["tools/".Length..]
                : null;
            if (tool is not null && ToolWindowHost.IsTool(tool))
            {
                ToolWindowHost.Open(tool);
                return;
            }
            _mainWindow?.NavigateTo(tab);
        }));
        HostCallbacks.OnShowTranslator += request => Dispatcher.BeginInvoke(new Action(() => ShowTranslatorWindow(request)));
        HostCallbacks.OnShowClipboard += visible => Dispatcher.BeginInvoke(new Action(() => ShowClipboardWindow(visible)));
    }

    private static void RestartProcess(bool asAdmin)
    {
        var processPath = Environment.ProcessPath;
        string fileName;
        string arguments;
        // `dotnet run`: ProcessPath is dotnet.exe → relaunch the app assembly under BaseDirectory.
        // Published single-file: ProcessPath is DevEnv.exe（Assembly.Location 在单文件下为空）。
        if (processPath is not null
            && Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var dll = Path.Combine(AppContext.BaseDirectory, "DevEnv.dll");
            if (!File.Exists(dll))
                return;
            fileName = processPath;
            arguments = $"\"{dll}\"";
        }
        else
        {
            fileName = processPath ?? string.Empty;
            arguments = string.Empty;
        }

        if (string.IsNullOrEmpty(fileName))
            return;

        try
        {
            if (asAdmin)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Environment.CurrentDirectory,
                });
            }
            else
            {
                // explorer 降权重启：去掉管理员令牌
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = string.IsNullOrEmpty(arguments) ? $"\"{fileName}\"" : $"\"{fileName}\" {arguments}",
                    UseShellExecute = true,
                });
            }

            if (System.Windows.Application.Current is App running)
                running.ExitApp();
        }
        catch (Win32Exception)
        {
            // UAC 已取消
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"无法重启: {ex.Message}", "程序盒子", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    #endregion

    #region 剪贴板历史

    private static void OnClipboardCaptured(ClipboardCapture capture)
    {
        try
        {
            if (capture.Kind == "text")
                _ = CoreServices.Clipboard.CaptureText(capture.Text ?? string.Empty);
            else if (capture.PngBytes is not null)
                _ = CoreServices.Clipboard.CaptureImage(capture.PngBytes);
        }
        catch
        {
            // 写库失败不影响主流程
        }
    }

    #endregion
}