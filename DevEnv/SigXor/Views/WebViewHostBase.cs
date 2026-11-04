using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using WebView2 = Microsoft.Web.WebView2.Wpf.WebView2;

namespace DevEnv.Host.SigXor.Views;

/// <summary>
/// WebView2 宿主基类：共享运行时探测、环境创建与生命周期。
/// 宿主可从可视树卸下后继续存活（由 <see cref="WebViewHostPool"/> 常驻复用），
/// 卸载时不销毁 Core，避免每次空格预览都重新拉起浏览器进程。
/// </summary>
public abstract class WebViewHostBase : System.Windows.Controls.UserControl, IDisposable
{
    /// <summary>webassets 虚拟主机域名（映射到打包内 webassets 目录）。</summary>
    public const string AssetsHost = "officeline.local";

    private static CoreWebView2Environment? _environment;
    private static readonly string UserDataFolder =
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "SigXor", "WebView2");

    private WebView2? _webView;
    private TaskCompletionSource<bool>? _readyTcs;
    private bool _disposed;
    protected string? _pendingNavigate;

    protected WebViewHostBase()
    {
        Loaded += OnLoaded;
    }

    private static bool? _runtimeAvailable;

    /// <summary>本机是否安装 WebView2 运行时（Evergreen / Fixed）；结果进程内缓存。</summary>
    public static bool IsRuntimeAvailable()
    {
        if (_runtimeAvailable is { } cached) return cached;
        try
        {
            _runtimeAvailable = CoreWebView2Environment.GetAvailableBrowserVersionString() is not null;
        }
        catch
        {
            _runtimeAvailable = false;
        }
        return _runtimeAvailable.Value;
    }

    /// <summary>CoreWebView2 是否已就绪（可立即 Navigate）。</summary>
    public bool IsCoreReady => !_disposed && _webView?.CoreWebView2 is not null;

    /// <summary>子类钩子：核心初始化后配置映射 / 拦截 / 导航策略。</summary>
    protected abstract void Configure(CoreWebView2 core);

    /// <summary>子类钩子：释放前解绑事件（与 Configure 对称）。</summary>
    protected virtual void OnDetach(CoreWebView2 core)
    {
    }

    /// <summary>子类钩子：WebView2 进程异常退出后的业务侧处理（切回结构化等）。</summary>
    protected virtual void OnEngineCrashed(string message)
    {
    }

    /// <summary>把解压后的 webassets 目录映射到虚拟主机 AssetsHost，供页面引用离线资源。</summary>
    protected static void MapAssets(CoreWebView2 core)
    {
        core.SetVirtualHostNameToFolderMapping(
            AssetsHost, WebAssets.Root, CoreWebView2HostResourceAccessKind.Allow);
    }

    /// <summary>预热共享 WebView2 环境（同进程同 UserDataFolder，仅创建一次）。</summary>
    public static async Task WarmEnvironmentAsync()
    {
        if (!IsRuntimeAvailable()) return;
        await GetEnvironmentAsync().ConfigureAwait(true);
    }

    /// <summary>共享的 WebView2 环境（同进程同 UserDataFolder，仅创建一次）。</summary>
    protected static async Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        _environment ??= await CoreWebView2Environment.CreateAsync(null, UserDataFolder);
        return _environment;
    }

    /// <summary>响应 /data 拦截时构造响应所需的共享环境（已由加载流程初始化）。</summary>
    protected static CoreWebView2Environment SharedEnvironment =>
        _environment ?? throw new InvalidOperationException("WebView2 环境尚未初始化");

    /// <summary>浏览器进程崩溃后丢弃失效 Environment，供下次重建。</summary>
    internal static void InvalidateSharedEnvironment() => _environment = null;

    /// <summary>已初始化的 Core；未就绪时为 null。</summary>
    protected CoreWebView2? Core => _webView?.CoreWebView2;

    /// <summary>请求导航；若核心未就绪则挂起，待加载完成后执行。</summary>
    protected void NavigateOrPending(string url)
    {
        if (_webView?.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.Navigate(url);
            _pendingNavigate = null;
        }
        else
        {
            _pendingNavigate = url;
        }
    }

    /// <summary>导航到空白页，释放当前文档句柄（归还池时调用）。</summary>
    public void NavigateBlank()
    {
        try
        {
            if (_webView?.CoreWebView2 is not null)
                _webView.CoreWebView2.Navigate("about:blank");
            else
                _pendingNavigate = null;
        }
        catch
        {
            // 进程异常时忽略
        }
    }

    /// <summary>归还池前清理路径/业务状态；默认清空挂起导航。子类可覆盖。</summary>
    public virtual void PrepareForReuse()
    {
        _pendingNavigate = null;
    }

    /// <summary>带缓存破坏参数的导航，避免复用宿主时同 URL 不刷新。</summary>
    protected static string WithCacheBust(string url)
    {
        char sep = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{sep}_={DateTime.UtcNow.Ticks}";
    }

    /// <summary>等待 Core 就绪；已就绪则立即完成。供预热与首次展示共用。</summary>
    public Task WaitUntilReadyAsync()
    {
        if (_disposed) return Task.FromException(new ObjectDisposedException(GetType().Name));
        if (IsCoreReady) return Task.CompletedTask;
        _readyTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // OnLoaded 可能在创建 TCS 的间隙完成
        if (IsCoreReady)
        {
            _readyTcs.TrySetResult(true);
            return Task.CompletedTask;
        }
        return _readyTcs.Task;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_disposed) return;

        // 常驻复用：已有 Core 时只需重新挂回可视树并冲刷挂起导航
        if (_webView is not null)
        {
            if (!ReferenceEquals(Content, _webView) && Content is not System.Windows.Controls.TextBlock)
                Content = _webView;

            if (_pendingNavigate is not null && _webView.CoreWebView2 is not null)
            {
                _webView.CoreWebView2.Navigate(_pendingNavigate);
                _pendingNavigate = null;
            }
            return;
        }

        try
        {
            _webView = new WebView2();
            Content = _webView;

            var environment = await GetEnvironmentAsync();
            if (_disposed)
            {
                SafeTearDownWebView();
                return;
            }

            await _webView.EnsureCoreWebView2Async(environment);
            if (_disposed)
            {
                SafeTearDownWebView();
                return;
            }

            var core = _webView.CoreWebView2;
            core.ProcessFailed += OnProcessFailed;
            Configure(core);

            if (_pendingNavigate is not null)
            {
                core.Navigate(_pendingNavigate);
                _pendingNavigate = null;
            }

            _readyTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _readyTcs.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _readyTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _readyTcs.TrySetException(ex);
            ShowFailure($"WebView2 初始化失败：{ex.Message}");
        }
    }

    /// <summary>渲染 / 浏览器进程崩溃时尽量保住宿主，并允许重建环境。</summary>
    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.BrowserProcessExited)
                    InvalidateSharedEnvironment();

                string message =
                    $"预览引擎异常退出（{e.ProcessFailedKind}）。已切回安全模式，请使用结构化预览或关闭后重试。";

                SafeTearDownWebView();
                ShowFailure(message);
                OnEngineCrashed(message);
                WebViewHostPool.NotifyHostCrashed(this);
            }
            catch
            {
                // 崩溃收尾绝不能再抛出，否则会拖垮整个 WPF 进程
            }
        });
    }

    private void SafeTearDownWebView()
    {
        var view = _webView;
        _webView = null;
        _readyTcs = null;
        if (view is null) return;
        try
        {
            if (view.CoreWebView2 is not null)
            {
                view.CoreWebView2.ProcessFailed -= OnProcessFailed;
                OnDetach(view.CoreWebView2);
            }
        }
        catch { /* 进程已死后解绑可能失败 */ }

        try { view.Dispose(); }
        catch { /* ignore */ }
    }

    private void ShowFailure(string message)
    {
        Content = new System.Windows.Controls.TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24),
            Foreground = System.Windows.Media.Brushes.DarkRed,
        };
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Loaded -= OnLoaded;
        SafeTearDownWebView();
    }
}
