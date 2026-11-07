using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows.Threading;
using DevEnv.Core.Services;
using DevEnv.Core.SigXor;
using DevEnv.Core.SigXor.Toolkit;
using DevEnv.Host.SigXor.ViewModels;
using DevEnv.Host.SigXor.Views;

namespace DevEnv.Host.SigXor.Services;

/// <summary>
/// 空格预览：全局键盘钩子捕获空格；前台为资源管理器且焦点不在输入框时，
/// 读取选中文件并弹出 Quick Look 式浮窗。也可由命令行 / 托盘直接打开文件预览。
/// </summary>
public sealed class ShellPreviewService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly BackupService _backup;
    private readonly Dispatcher _dispatcher;
    private readonly IDisposable _hotkeyRegistration;
    private readonly ExplorerSelectionService _selection = new();
    private readonly DesktopSelectionService _desktopSelection = new();
    // 快捷应用同时搜索全局启动器里的本地应用 / 文件夹 / 网站
    private readonly LauncherService _launcher = new(CoreServices.Shortcuts);
    private readonly Action _openSettings;
    private ShellPreviewViewModel? _viewModel;
    private ShellPreviewWindow? _window;
    private LauncherWindow? _launcherWindow;
    /// <summary>最近一次预览的资源管理器选中项，用于区分「再按空格关闭」和「换了文件继续预览」。</summary>
    private string _lastSelectionKey = "";
    private bool _disposed;

    public ShellPreviewService(
        SettingsService settings,
        BackupService backup,
        Dispatcher dispatcher,
        GlobalHotkeyRouter router,
        Action openSettings)
    {
        _settings = settings;
        _backup = backup;
        _dispatcher = dispatcher;
        _openSettings = openSettings;
        _hotkeyRegistration = router.Register(100, OnKeyPressed, OnKeyReleased);
    }

    public bool IsPreviewVisible => _window is { IsVisible: true };

    /// <summary>
    /// 预览窗的空格 / Esc 是否仍该由我们处理：预览窗持有前台，或用户还在资源管理器里
    /// （保持「再按空格 / Esc 关闭预览」的习惯）。焦点落到其他应用后一律放行，
    /// 避免在别处打字或按 Esc 时把预览误关。
    /// </summary>
    private bool ShouldHandlePreviewCloseKey()
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        if (_window is { IsVisible: true }
            && foreground == new WindowInteropHelper(_window).Handle)
        {
            return true;
        }

        // 资源管理器文件列表或桌面：空格 / Esc 由预览接管
        string? className = GetWindowClassName(foreground);
        if (!InputFocusClassifier.IsExplorerClass(className)
            && !InputFocusClassifier.IsDesktopClass(className))
        {
            return false;
        }

        // 焦点在资源管理器搜索框等输入控件里时，空格/Esc 属于输入，不拦截
        return !IsInputFocusOnTextBox(foreground);
    }

    private bool OnKeyPressed(int vkCode)
    {
        PreviewSettings preview = _settings.Current.Preview;
        int closeKey = ShortcutDefinition.ToPreviewVirtualKey(
            preview.CloseShortcut, GlobalHotkeyService.VK_ESCAPE);
        int openKey = ShortcutDefinition.ToPreviewVirtualKey(
            preview.OpenShortcut, GlobalHotkeyService.VK_SPACE);

        if (vkCode == closeKey)
        {
            if (!IsPreviewVisible || !ShouldHandlePreviewCloseKey()) return false;
            _dispatcher.BeginInvoke(ClosePreview);
            return true;
        }

        if (vkCode != openKey) return false;

        // 启动器已打开：不拦截预览键，让搜索框正常输入
        if (_launcherWindow is { IsVisible: true }) return false;

        // 浮窗已打开：预览窗持有前台或在资源管理器里时，再按预览键处理（吞掉按键，避免传给 Explorer）。
        // 失焦到其他应用后不拦截，预览键交给当前应用，避免在别处打字时预览被误关。
        // 编辑模式下同样放行，保证能输入预览键。
        if (IsPreviewVisible)
        {
            if (_window?.IsInEditMode() == true) return false;
            if (ShouldHandlePreviewCloseKey())
            {
                IntPtr current = GetForegroundWindow();
                if (_window is { } window && current == new WindowInteropHelper(window).Handle)
                {
                    // 预览窗自己在前台：空格就是关闭
                    _dispatcher.BeginInvoke(ClosePreview);
                    return true;
                }

                // 焦点回到资源管理器：重新读取选中项。选中项没变才关闭，
                // 换了文件则把新文件加入历史并直接预览，不需要先关窗再按一次空格。
                IntPtr captured = current;
                _dispatcher.BeginInvoke(() => ShowPreviewFromExplorer(captured, toggleIfUnchanged: true));
                return true;
            }
        }

        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        string? className = GetWindowClassName(foreground);

        // 资源管理器或桌面：空格预览选中的文件
        if (InputFocusClassifier.IsExplorerClass(className)
            || InputFocusClassifier.IsDesktopClass(className))
        {
            if (IsInputFocusOnTextBox(foreground)) return false;
            IntPtr captured = foreground;
            _dispatcher.BeginInvoke(() => ShowPreviewFromExplorer(captured));
            return true;
        }

        return false;
    }

    private bool OnKeyReleased(int vkCode) => false;

    private void ShowPreviewFromExplorer(IntPtr foreground, bool toggleIfUnchanged = false)
    {
        if (_disposed) return;

        bool onDesktop = InputFocusClassifier.IsDesktopClass(GetWindowClassName(foreground));
        var paths = onDesktop
            ? _desktopSelection.GetSelectedFiles()
            : _selection.GetSelectedFiles(foreground);
        if (paths.Count == 0)
        {
            if (toggleIfUnchanged && IsPreviewVisible) ClosePreview();
            return;
        }

        if (toggleIfUnchanged && IsPreviewVisible && IsSelectionUnchanged(paths))
        {
            ClosePreview();
            return;
        }

        ShowPreviewForPaths(paths);
    }

    /// <summary>资源管理器里的选中项是否仍是上一次预览的那一批（用于空格切换关闭）。</summary>
    private bool IsSelectionUnchanged(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1 && _viewModel?.Preview.CurrentFile is { } current
            && string.Equals(paths[0], current.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(SelectionKey(paths), _lastSelectionKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string SelectionKey(IEnumerable<string> paths) =>
        string.Join('\n', paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));

    /// <summary>按路径列表打开预览浮窗（托盘「打开文件」、命令行参数、单例转发）。</summary>
    public void ShowPreviewForPaths(IEnumerable<string> paths)
    {
        if (_disposed) return;

        var files = BuildEntries(paths);
        if (files.Count == 0) return;

        bool wasVisible = _window is { IsVisible: true };
        _viewModel ??= new ShellPreviewViewModel(_settings, _backup);
        _window ??= new ShellPreviewWindow { DataContext = _viewModel };

        // 窗口没关时把新文件追加到历史，之前预览过的文件仍可用 ← 返回；
        // 首次打开则重置历史。
        if (wasVisible)
            _viewModel.AppendFiles(files);
        else
            _viewModel.ResetFiles(files);

        _lastSelectionKey = SelectionKey(files.Select(f => f.FullPath));

        if (!wasVisible)
        {
            _window.Show();
            _window.Activate();
        }
        else
        {
            // 已经打开时只刷新内容，不抢焦点，方便继续在资源管理器里选下一个文件
            _window.Show();
        }
    }

    private static List<FileEntry> BuildEntries(IEnumerable<string> paths)
    {
        var files = new List<FileEntry>();
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (Directory.Exists(path))
            {
                var di = new DirectoryInfo(path);
                files.Add(new FileEntry
                {
                    Name = di.Name,
                    FullPath = di.FullName,
                    IsDirectory = true,
                    LastWriteTime = di.LastWriteTime,
                });
                continue;
            }

            if (!File.Exists(path)) continue;
            var fi = new FileInfo(path);
            files.Add(new FileEntry
            {
                Name = fi.Name,
                FullPath = fi.FullName,
                Length = fi.Length,
                LastWriteTime = fi.LastWriteTime,
                Kind = FileTypeDetector.Detect(fi.FullName),
            });
        }
        return files;
    }

    public void ClosePreview()
    {
        if (_window is null) return;
        _window.Hide();
        _viewModel?.DisposeContent();
        _lastSelectionKey = "";
    }

    /// <summary>
    /// 启动期预热启动器：后台建立应用索引，并预先构造窗口。
    /// 首次左键点托盘时不再同步扫描开始菜单、不卡界面。
    /// </summary>
    public void WarmLauncher()
    {
        if (_disposed) return;
        _ = _launcher.WarmupAsync();
        _launcherWindow ??= new LauncherWindow(_launcher, _openSettings);
    }

    /// <summary>显示快捷应用（Alt+Space 全局热键，或托盘左键点击）。</summary>
    public void ShowLauncher()
    {
        if (_disposed) return;
        _launcherWindow ??= new LauncherWindow(_launcher, _openSettings);
        _launcherWindow.ShowLauncher();
    }

    private static bool IsInputFocusOnTextBox(IntPtr window)
    {
        try
        {
            _ = GetWindowThreadProcessId(window, out uint processId);
            _ = processId;
            uint threadId = GetWindowThreadProcessId(window, out _);
            if (threadId == 0) return false;

            var info = new GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() };
            if (!GetGUIThreadInfo(threadId, ref info)) return false;
            if (info.hwndFocus == IntPtr.Zero) return false;

            string? focusClass = GetWindowClassName(info.hwndFocus);
            return InputFocusClassifier.IsLikelyInputControl(focusClass);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetWindowClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        int len = GetClassName(hwnd, sb, sb.Capacity);
        return len == 0 ? null : sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hotkeyRegistration.Dispose();
        _viewModel?.DisposeContent();
        _window?.Close();
        _launcherWindow?.Close();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
