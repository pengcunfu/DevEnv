using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DevEnv.Core;
using DevEnv.Host.WebView;
using DevEnv.Core.Translator.Models;
using DevEnv.Core.Translator.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using WpfClipboard = System.Windows.Clipboard;

namespace DevEnv.Host.Windows;

/// <summary>
/// 熔岩翻译独立窗：WPF 外壳 + WebView2 承载翻译站（DOM 填充必须真实浏览器）。
/// DOM 填充脚本使用 Core 的 Translator 模块 <c>WebFillAutomation</c>，未作改动。
/// </summary>
public partial class TranslatorWindow : Window
{
    private readonly ConfigService _config = new();

    private readonly WebView2 _browser = new();
    private CoreWebView2Environment? _env;
    private TaskCompletionSource<bool>? _readyTcs;
    private string? _loadedSiteId;
    private bool _ready;
    private bool _initialized;
    private bool _handlersWired;
    private bool _filling;
    private bool _suppressSiteChange;
    private bool _allowClose;

    public TranslatorWindow()
    {
        InitializeComponent();

        // WebView2 控件放最后，避免盖住占位提示
        _browser.Visibility = Visibility.Collapsed;
        BrowserHost.Children.Add(_browser);
        SiteCombo.ItemsSource = WebTranslateCatalog.All;

        Closed += (_, _) => TearDown();

        foreach (var site in WebTranslateCatalog.All)
        {
            if (string.Equals(site.Id, CurrentSite().Id, StringComparison.OrdinalIgnoreCase))
            {
                _suppressSiteChange = true;
                SiteCombo.SelectedItem = site;
                _suppressSiteChange = false;
                break;
            }
        }
    }

    /// <summary>点关闭只隐藏；进程退出前需 <see cref="PrepareForExit"/> 放行。</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
            return;
        e.Cancel = true;
        Hide();
        TearDown();
    }

    public void PrepareForExit() => _allowClose = true;

    public void AttachEnvironment(CoreWebView2Environment env) => _env = env;

    #region 显示 / 隐藏

    public void ShowAndActivate()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    public void HideAndRelease()
    {
        if (IsVisible)
            Hide();
        TearDown();
    }

    #endregion

    /// <summary>按请求应用：切站点 / 打开首页 / 用剪贴板文本填充。</summary>
    public async void Apply(TranslatorRequest request)
    {
        await EnsureReadyAsync();

        if (!request.Visible)
        {
            HideAndRelease();
            return;
        }

        ShowAndActivate();

        if (!string.IsNullOrWhiteSpace(request.SiteId))
        {
            SetDefaultSite(request.SiteId);
        }

        var site = CurrentSite();
        var text = request.Text;
        if (request.FillClipboard && string.IsNullOrWhiteSpace(text))
            text = TryGetClipboardText();

        if (!string.IsNullOrWhiteSpace(text))
        {
            await FillAsync(text);
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.Url)
            && Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, site.HomeUrl.Host, StringComparison.OrdinalIgnoreCase)
            && !IsSamePage(uri, site.HomeUrl))
        {
            _ready = false;
            _browser.CoreWebView2?.Navigate(uri.ToString());
            _loadedSiteId = site.Id;
            SetStatus($"已打开 {site.Name}");
            return;
        }

        // EnsureReadyAsync 不再导航；这里负责首次 / 切站。已在加载同站时不要再 Navigate，
        // 否则会中止上一趟导航，NavigationCompleted 报 ConnectionAborted。
        if (IsSameLoaded(site))
            SetStatus($"已打开 {site.Name}");
        else if (!IsLoadingSite(site))
            NavigateHome(site);
    }

    private bool IsLoadingSite(WebTranslateSite site) =>
        !_ready && string.Equals(_loadedSiteId, site.Id, StringComparison.OrdinalIgnoreCase);

    private void OnSiteChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSiteChange)
            return;
        if (SiteCombo.SelectedItem is not WebTranslateSite site)
            return;
        _ = SwitchSiteAsync(site);
    }

    private async Task SwitchSiteAsync(WebTranslateSite site)
    {
        await EnsureReadyAsync();
        SetDefaultSite(site.Id);
        NavigateHome(site);
    }

    private void SetDefaultSite(string siteId)
    {
        var site = WebTranslateCatalog.FindById(siteId) ?? WebTranslateCatalog.All[0];
        var general = _config.Current.General;
        general.LastWebSiteId = site.Id;
        _config.UpdateGeneral(general);

        _suppressSiteChange = true;
        SiteCombo.SelectedItem = site;
        _suppressSiteChange = false;
    }

    private WebTranslateSite CurrentSite() =>
        WebTranslateCatalog.FindById(_config.Current.General.LastWebSiteId) ?? WebTranslateCatalog.All[0];


    #region WebView2 生命周期

    private async Task EnsureReadyAsync()
    {
        if (_initialized)
            return;

        _env ??= await WebView2EnvironmentFactory.GetSharedAsync();
        await _browser.EnsureCoreWebView2Async(_env);

        WebView2Ui.ConfigureShell(
            _browser.CoreWebView2,
#if DEBUG
            enableDevTools: true,
#else
            enableDevTools: false,
#endif
            allowExternalDrop: false,
            customNativeMenu: true,
            contextMenuTarget: BrowserHost);

        // 控件生命周期内只挂一次：TearDown 只卸内容，不销毁 CoreWebView2
        if (!_handlersWired)
        {
            _browser.CoreWebView2.NavigationStarting += (_, _) =>
            {
                if (_initialized)
                    _ready = false;
            };
            _browser.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _handlersWired = true;
        }

        _initialized = true;
        WebView2EnvironmentFactory.AddRef();
        _browser.Visibility = Visibility.Visible;
        Placeholder.Visibility = Visibility.Collapsed;
        // 不在这里 Navigate：Apply / SwitchSite / Fill 会接着导航；
        // 这里再 Navigate 会被紧跟着的第二次 Navigate 中止，首次打开就会误报 ConnectionAborted。
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        // TearDown 后的 about:blank / 被顶替的导航一律忽略，避免首次打开残留红字
        if (!_initialized)
            return;

        if (!e.IsSuccess)
        {
            // 被新导航顶替时不要结束 WaitReady，也不要刷红字；等后续成功/真失败
            if (IsTransientNavigationFailure(e.WebErrorStatus))
                return;

            _ready = false;
            _readyTcs?.TrySetResult(false);
            _readyTcs = null;
            SetStatus($"网页加载失败（{e.WebErrorStatus}），请检查网络或 WebView2 运行时", error: true);
            return;
        }

        _ready = true;
        _readyTcs?.TrySetResult(true);
        _readyTcs = null;
        await InjectThinScrollbarAsync();
        var site = CurrentSite();
        if (IsSameLoaded(site))
            SetStatus($"已加载 {site.Name}");
    }

    private static bool IsTransientNavigationFailure(CoreWebView2WebErrorStatus status) =>
        status is CoreWebView2WebErrorStatus.ConnectionAborted
            or CoreWebView2WebErrorStatus.OperationCanceled;

    /// <summary>隐藏时销毁控件：翻译站很重，拆掉控件才能让 renderer 真正退出。</summary>
    private void TearDown()
    {
        if (!_initialized)
            return;
        _initialized = false;
        _ready = false;
        _loadedSiteId = null;
        _readyTcs = null;

        _browser.Visibility = Visibility.Collapsed;
        _browser.CoreWebView2?.Navigate("about:blank");
        WebView2EnvironmentFactory.Release();
    }

    private void NavigateHome(WebTranslateSite site)
    {
        if (_browser.CoreWebView2 is null)
            return;
        _ready = false;
        _browser.CoreWebView2.Navigate(site.HomeUrl.ToString());
        _loadedSiteId = site.Id;
        SetStatus($"已加载 {site.Name}");
    }

    private bool IsSameLoaded(WebTranslateSite site) =>
        string.Equals(_loadedSiteId, site.Id, StringComparison.OrdinalIgnoreCase) && _ready;

    #endregion

    #region 填充剪贴板

    private async void OnFillClick(object sender, RoutedEventArgs e)
    {
        var text = TryGetClipboardText();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("剪贴板无文本", warning: true);
            return;
        }
        await FillAsync(text);
    }

    private async Task FillAsync(string text)
    {
        if (_filling)
            return;
        _filling = true;
        try
        {
            await EnsureReadyAsync();
            var site = CurrentSite();
            if (!_ready || !IsSameLoaded(site))
            {
                SetStatus($"正在打开 {site.Name}…");
                NavigateHome(site);
                if (!await WaitReadyAsync(TimeSpan.FromSeconds(25)))
                {
                    SetStatus("网页加载超时，请稍后重试", warning: true);
                    return;
                }
            }

            Exception? lastError = null;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    if (_browser.CoreWebView2 is null)
                        break;
                    var json = await _browser.CoreWebView2.ExecuteScriptAsync(
                        WebFillAutomation.BuildFillScript(site.Id, text));
                    if (TryParseFillResult(json, out var ok, out var detail))
                    {
                        if (ok)
                        {
                            SetStatus($"已把剪贴板内容填入 {site.Name}");
                            return;
                        }
                        lastError = new InvalidOperationException(detail);
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    break;
                }

                await Task.Delay(400);
            }

            SetStatus($"未能自动填入 {site.Name}（{lastError?.Message ?? "未知原因"}），可手动 Ctrl+V 粘贴", warning: true);
        }
        finally
        {
            _filling = false;
        }
    }

    private async Task<bool> WaitReadyAsync(TimeSpan timeout)
    {
        if (_ready)
            return true;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _readyTcs = tcs;
        if (_ready)
        {
            _readyTcs = null;
            return true;
        }
        var winner = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        return ReferenceEquals(winner, tcs.Task) && tcs.Task.Result;
    }

    private static bool TryParseFillResult(string? json, out bool ok, out string detail)
    {
        ok = false;
        detail = "未知原因";
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            if (json.Length >= 2 && json[0] == '"' && json[^1] == '"')
                json = JsonSerializer.Deserialize<string>(json) ?? json;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True)
                ok = true;
            if (doc.RootElement.TryGetProperty("detail", out var detailEl))
                detail = detailEl.GetString() ?? detail;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryGetClipboardText()
    {
        try
        {
            return WpfClipboard.ContainsText() ? WpfClipboard.GetText()?.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    private static bool IsSamePage(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port
        && string.Equals(a.AbsolutePath, b.AbsolutePath, StringComparison.OrdinalIgnoreCase);

    private async Task InjectThinScrollbarAsync()
    {
        const string script = """
            (function () {
              const id = 'lava-thin-scrollbar';
              if (document.getElementById(id)) return;
              const style = document.createElement('style');
              style.id = id;
              style.textContent = `
                html { scrollbar-width: thin; }
                ::-webkit-scrollbar { width: 5px; height: 5px; }
                ::-webkit-scrollbar-track { background: transparent; }
                ::-webkit-scrollbar-thumb { background: rgba(0,0,0,.28); border-radius: 3px; }
                ::-webkit-scrollbar-thumb:hover { background: rgba(0,0,0,.42); }
              `;
              (document.head || document.documentElement).appendChild(style);
            })();
            """;
        try
        {
            if (_browser.CoreWebView2 is not null)
                await _browser.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch
        {
            // 被站点 CSP 拦下，忽略
        }
    }

    private void SetStatus(string message, bool warning = false, bool error = false)
    {
        void Apply()
        {
            StatusText.Text = message;
            StatusText.Foreground = error
                ? (System.Windows.Media.Brush)FindResource("DangerBrush")
                : warning
                    ? (System.Windows.Media.Brush)FindResource("WarningBrush")
                    : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        }

        if (Dispatcher.CheckAccess())
            Apply();
        else
            Dispatcher.BeginInvoke(Apply);
    }
}