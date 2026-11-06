using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DevEnv.Core;
using DevEnv.Host.SigXor.Services;

namespace DevEnv.Host.SigXor.Views;

/// <summary>
/// 快捷应用窗口：搜索应用、文件，执行命令，拖拽固定应用。
/// </summary>
public partial class LauncherWindow : Window
{
    /// <summary>窗口显示后允许的抢焦点宽限期；超过后仍未激活就视为应关闭。</summary>
    private const double FocusGraceMs = 600;

    private readonly LauncherService _launcher;
    private readonly Action _openSettings;
    private readonly DispatcherTimer _debounce;
    private readonly DispatcherTimer _focusWatch;
    private List<LauncherResult> _results = [];
    private string _lastQuery = "";
    /// <summary>最近一次 ShowLauncher 的时间；用于忽略抢焦点瞬间的假 Deactivated。</summary>
    private DateTime _shownAtUtc;

    public LauncherWindow(LauncherService launcher, Action openSettings)
    {
        _launcher = launcher;
        _openSettings = openSettings;
        InitializeComponent();
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _debounce.Tick += OnDebounceTick;
        // 兜底：窗口可见但未激活时（点击了窗口外）主动收起，避免卡在“看得见点不掉”的状态
        _focusWatch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _focusWatch.Tick += OnFocusWatchTick;
    }

    /// <summary>显示启动器并聚焦搜索框。</summary>
    public void ShowLauncher()
    {
        if (!IsVisible)
        {
            SearchBox.Text = "";
            Show();
        }
        UpdateSearchIcon();
        ShowPopularApps();
        _shownAtUtc = DateTime.UtcNow;
        // 从托盘或全局热键唤起时普通 Activate 常失败，窗口会立刻 Deactivated 并被关掉。
        WindowPlacement.ForceForeground(this);
        _focusWatch.Start();
        SearchBox.Focus();
    }

    private void ShowPopularApps()
    {
        _results = _launcher.GetPopularApps();
        _lastQuery = "";
        ResultsList.ItemsSource = _results.Select(r => new LauncherItemViewModel(r)).ToList();
        EmptyHint.Visibility = _results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CategoryLabel.Text = _results.Count > 0
            ? "快捷应用"
            : (_launcher.HasAppCache ? "" : "正在加载应用…");
        if (_results.Count > 0) ResultsList.SelectedIndex = 0;

        // 索引还在后台构建时先显示固定应用，完成后自动补全列表
        if (!_launcher.HasAppCache) _ = RefreshWhenCacheReadyAsync();
    }

    /// <summary>后台索引就绪后刷新当前界面（无查询时补全快捷应用，有查询时重跑搜索）。</summary>
    private async Task RefreshWhenCacheReadyAsync()
    {
        try
        {
            await _launcher.WarmupAsync();
        }
        catch
        {
            return;
        }

        if (!IsVisible) return;
        // 构建失败时保持当前列表，避免再次触发刷新造成循环
        if (!_launcher.HasAppCache) return;

        string query = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            ShowPopularApps();
        }
        else if (query == _lastQuery)
        {
            RunSearch(query);
        }
    }

    #region 搜索

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchIcon();
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>命令模式（以 > 开头）时把搜索图标换成命令提示符，槽位保持正方形。</summary>
    private void UpdateSearchIcon()
    {
        bool commandMode = SearchBox.Text.TrimStart().StartsWith('>');
        SearchIcon.Text = commandMode ? "" : "";
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        string query = SearchBox.Text.Trim();
        if (query == _lastQuery) return;
        _lastQuery = query;

        if (string.IsNullOrWhiteSpace(query))
        {
            ShowPopularApps();
            return;
        }

        RunSearch(query);
    }

    private void RunSearch(string query)
    {
        _results = _launcher.Search(query);
        ResultsList.ItemsSource = _results.Select(r => new LauncherItemViewModel(r)).ToList();
        EmptyHint.Visibility = _results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_results.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            int appCount = _results.Count(r => r.Category == LauncherCategory.Application);
            int folderCount = _results.Count(r => r.Category == LauncherCategory.Folder);
            int webCount = _results.Count(r => r.Category == LauncherCategory.Website);
            int fileCount = _results.Count(r => r.Category == LauncherCategory.File);
            int cmdCount = _results.Count(r => r.Category == LauncherCategory.Command);
            var parts = new List<string>();
            if (appCount > 0) parts.Add($"{appCount} 个应用");
            if (folderCount > 0) parts.Add($"{folderCount} 个文件夹");
            if (webCount > 0) parts.Add($"{webCount} 个网站");
            if (fileCount > 0) parts.Add($"{fileCount} 个文件");
            if (cmdCount > 0) parts.Add($"{cmdCount} 个命令");
            CategoryLabel.Text = string.Join(" · ", parts);
        }
        else
        {
            CategoryLabel.Text = "";
        }
    }

    #endregion

    #region 常用工具入口

    /// <summary>剪贴板入口：先收起快捷应用，再打开剪贴板窗口。</summary>
    private void OnOpenClipboardEntry(object sender, RoutedEventArgs e)
    {
        CloseLauncher();
        HostCallbacks.OnShowClipboard?.Invoke(true);
    }

    /// <summary>翻译入口：沿用原翻译热键的行为，打开翻译窗并填充剪贴板文本。</summary>
    private void OnOpenTranslatorEntry(object sender, RoutedEventArgs e)
    {
        CloseLauncher();
        HostCallbacks.OnShowTranslator?.Invoke(new TranslatorRequest
        {
            Visible = true,
            FillClipboard = true,
        });
    }

    /// <summary>开发环境入口：收起快捷应用并显示主窗口。</summary>
    private void OnOpenMainWindowEntry(object sender, RoutedEventArgs e)
    {
        CloseLauncher();
        HostCallbacks.OnShowMainWindow?.Invoke();
    }

    /// <summary>文件浏览器入口：收起快捷应用并打开文件浏览器窗口。</summary>
    private void OnOpenBrowserEntry(object sender, RoutedEventArgs e)
    {
        CloseLauncher();
        HostCallbacks.OnOpenSigXorBrowser?.Invoke();
    }

    #endregion

    #region 列表交互

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedIndex >= 0 && ResultsList.SelectedIndex < _results.Count)
        {
            EmptyHint.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>右键先把指针下的那一项设为选中，菜单操作才会落在它身上。</summary>
    private void OnResultsRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (ResultsList.ContainerFromElement(source) is not ListBoxItem container) return;
        int index = ResultsList.ItemContainerGenerator.IndexFromContainer(container);
        if (index >= 0) ResultsList.SelectedIndex = index;
    }

    /// <summary>
    /// 右键菜单按指针下的项决定可执行的操作；空白处和搜索框不弹菜单。
    /// 键盘菜单键（光标坐标为空）回退到当前选中项。
    /// </summary>
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        int index = ResolveContextIndex(e);
        if (index < 0)
        {
            e.Handled = true;
            return;
        }

        if (ResultsList.SelectedIndex != index) ResultsList.SelectedIndex = index;

        var item = _results[index];
        bool isCommand = item.Category == LauncherCategory.Command;
        bool isFile = item.Category == LauncherCategory.File;
        bool isFolder = item.Category == LauncherCategory.Folder;
        bool isWebsite = item.Category == LauncherCategory.Website;
        bool canRunElevated = item.Category == LauncherCategory.Application
            || isCommand
            || (isFile && LauncherService.IsExecutablePath(item.Path));

        RunMenuItem.Header = isFolder ? "打开文件夹"
            : isWebsite ? "打开网站"
            : isFile ? "打开"
            : isCommand ? "运行命令"
            : "运行";
        RunAsAdminMenuItem.Visibility = canRunElevated ? Visibility.Visible : Visibility.Collapsed;
        RevealMenuItem.Visibility = !isCommand && !isWebsite
                                    && (File.Exists(item.Path) || Directory.Exists(item.Path))
            ? Visibility.Visible
            : Visibility.Collapsed;

        PinMenuItem.Visibility = _launcher.CanPin(item) ? Visibility.Visible : Visibility.Collapsed;
        RemovePinnedMenuItem.Visibility = IsPinnedIndex(index) ? Visibility.Visible : Visibility.Collapsed;
        PinSeparator.Visibility = PinMenuItem.Visibility == Visibility.Visible
                               || RemovePinnedMenuItem.Visibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private int ResolveContextIndex(ContextMenuEventArgs e)
    {
        bool viaKeyboard = e.CursorLeft < 0 || e.CursorTop < 0;
        if (viaKeyboard)
            return ResultsList.IsKeyboardFocusWithin ? ResultsList.SelectedIndex : -1;

        var source = e.OriginalSource as DependencyObject;
        if (source is null) return -1;
        if (ResultsList.ContainerFromElement(source) is not ListBoxItem container) return -1;
        return ResultsList.ItemContainerGenerator.IndexFromContainer(container);
    }

    private bool IsPinnedIndex(int index) =>
        index >= 0 && index < _results.Count && _launcher.IsPinned(_results[index].Path);

    private LauncherResult? SelectedResult()
    {
        int index = ResultsList.SelectedIndex;
        return index >= 0 && index < _results.Count ? _results[index] : null;
    }

    private void OnRunSelected(object sender, RoutedEventArgs e) => ExecuteSelected();

    private void OnRunAsAdminSelected(object sender, RoutedEventArgs e)
    {
        if (SelectedResult() is not { } result) return;
        CloseLauncher();
        _launcher.ExecuteAsAdministrator(result);
    }

    private void OnRevealSelected(object sender, RoutedEventArgs e)
    {
        if (SelectedResult() is not { } result) return;
        CloseLauncher();
        _launcher.Reveal(result);
    }

    private void OnPinSelected(object sender, RoutedEventArgs e)
    {
        if (SelectedResult() is not { } result) return;
        _launcher.AddPinnedApp(result.Path);
        RefreshCurrentResults();
    }

    /// <summary>固定/取消固定后刷新列表：空搜索刷快捷应用，有查询则重跑当前搜索。</summary>
    private void RefreshCurrentResults()
    {
        string query = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            ShowPopularApps();
            return;
        }

        _lastQuery = query;
        RunSearch(query);
    }

    /// <summary>快捷应用按单击操作：点中哪一项就立即启动，不再要求双击。</summary>
    private void OnResultClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is not DependencyObject source) return;
        if (ResultsList.ContainerFromElement(source) is not ListBoxItem container) return;

        int index = ResultsList.ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0 || index >= _results.Count) return;

        ResultsList.SelectedIndex = index;
        e.Handled = true;
        ExecuteSelected();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                CloseLauncher();
                break;

            case Key.Enter:
                e.Handled = true;
                ExecuteSelected();
                break;

            case Key.Up:
                e.Handled = true;
                if (ResultsList.SelectedIndex > 0)
                    ResultsList.SelectedIndex--;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                break;

            case Key.Down:
                e.Handled = true;
                if (ResultsList.SelectedIndex < _results.Count - 1)
                    ResultsList.SelectedIndex++;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                break;

            case Key.Tab:
                e.Handled = true;
                if (ResultsList.SelectedIndex < _results.Count - 1)
                    ResultsList.SelectedIndex++;
                else
                    ResultsList.SelectedIndex = 0;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                break;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // 从任务栏抢焦点时，Show/Activate 落定前可能先收到一次 Deactivated。
            if ((DateTime.UtcNow - _shownAtUtc).TotalMilliseconds < 400) return;
            if (!IsLauncherForeground()) CloseLauncher();
        }, DispatcherPriority.Input);
    }

    /// <summary>
    /// 焦点看护：前台不再是启动器窗口时收起。
    /// 不依赖 IsActive / Deactivated：ForceForeground 走的是 AttachThreadInput，
    /// WPF 的激活状态可能与之不同步，Deactivated 可能根本不触发。
    /// </summary>
    private void OnFocusWatchTick(object? sender, EventArgs e)
    {
        if (!IsVisible)
        {
            _focusWatch.Stop();
            return;
        }

        if ((DateTime.UtcNow - _shownAtUtc).TotalMilliseconds < FocusGraceMs) return;
        if (!IsLauncherForeground()) CloseLauncher();
    }

    /// <summary>当前前台窗口是否就是启动器本身（菜单等本进程弹出窗口不会抢前台）。</summary>
    private bool IsLauncherForeground()
    {
        // 右键菜单打开期间不要收起启动器（个别环境下弹出菜单会接管前台）
        if (ResultsContextMenu?.IsOpen == true) return true;

        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return true; // 窗口切换途中，先不判负
        return foreground == new WindowInteropHelper(this).Handle;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private void ExecuteSelected()
    {
        int index = ResultsList.SelectedIndex;
        if (index < 0 || index >= _results.Count) return;
        var result = _results[index];
        CloseLauncher();
        _launcher.Execute(result);
    }

    private void CloseLauncher()
    {
        _debounce.Stop();
        _focusWatch.Stop();
        Hide();
    }

    /// <summary>设置入口：先收起启动器，再打开设置窗口。</summary>
    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        CloseLauncher();
        _openSettings();
    }

    #endregion

    #region 取消固定

    private void OnRemovePinned(object sender, RoutedEventArgs e)
    {
        if (SelectedResult() is not { } result) return;
        _launcher.RemovePinnedApp(result.Path);
        RefreshCurrentResults();
    }

    #endregion

    #region 拖拽

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            // 显示拖拽叠加层
            DropOverlay.Opacity = 0.9;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        // 隐藏拖拽叠加层
        var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        DropOverlay.BeginAnimation(OpacityProperty, anim);
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        // 隐藏拖拽叠加层
        DropOverlay.Opacity = 0;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0) return;

        foreach (string file in files)
        {
            if (!File.Exists(file)) continue;
            // 只固定可执行文件和快捷方式
            string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".exe" or ".lnk" or ".msc" or ".bat" or ".cmd")
            {
                _launcher.AddPinnedApp(file);
            }
        }

        // 刷新列表
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            ShowPopularApps();
        }

        e.Handled = true;
    }

    #endregion
}

/// <summary>启动器列表项的显示模型。</summary>
public sealed class LauncherItemViewModel(LauncherResult result)
{
    public string Name => result.Name;
    public string Description => result.Description ?? result.Path;

    /// <summary>真实图标（从 exe/lnk 提取）。</summary>
    public BitmapSource? Icon => result.Icon;

    /// <summary>是否有真实图标。</summary>
    public Visibility HasIcon => result.Icon is not null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>字体图标（无真实图标时的回退）。</summary>
    public string FontIcon => result.Category switch
    {
        LauncherCategory.Application => " ",
        LauncherCategory.File => " ",
        LauncherCategory.Folder => "",
        LauncherCategory.Website => "",
        LauncherCategory.Command => "",
        _ => " ",
    };

    public string CategoryText => result.Category switch
    {
        LauncherCategory.Application => "应用",
        LauncherCategory.File => "文件",
        LauncherCategory.Folder => "文件夹",
        LauncherCategory.Website => "网站",
        LauncherCategory.Command => "命令",
        _ => "",
    };
}
