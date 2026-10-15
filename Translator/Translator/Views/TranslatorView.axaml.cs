using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using LavaTranslator.Models;
using LavaTranslator.Services;

namespace LavaTranslator.Views;

public partial class TranslatorView : UserControl
{
    private readonly ConfigService _configService;
    private bool _webInitialized;
    private bool _webReady;
    private TaskCompletionSource<bool>? _webReadyTcs;
    private bool _attachedToVisual;
    private string? _loadedSiteId;

    /// <summary>请求宿主切换到本 Tab 并显示主窗口（由 DevBox 注入）。</summary>
    public Action? RequestActivateHost { get; set; }

    public TranslatorView()
        : this(new ConfigService())
    {
    }

    public TranslatorView(ConfigService configService)
    {
        _configService = configService;
        InitializeComponent();
        _configService.ConfigChanged += OnConfigChanged;

        AttachedToVisualTree += (_, _) =>
        {
            if (_attachedToVisual)
                return;
            _attachedToVisual = true;
            // 等视觉树就绪后再导航，避免 WebView2 过早初始化
            Dispatcher.UIThread.Post(NavigateSelectedWebSite, DispatcherPriority.Loaded);
        };
    }

    /// <summary>供 DevBox 全局热键回调。</summary>
    public void HandleHotkeyPressed()
        => Dispatcher.UIThread.Post(OnHotkeyPressed);

    private WebTranslateSite CurrentWebSite =>
        WebTranslateCatalog.FindById(_configService.Current.General.LastWebSiteId)
        ?? WebTranslateCatalog.All[0];

    private void OnConfigChanged(object? sender, EventArgs e)
        => Dispatcher.UIThread.Post(NavigateSelectedWebSite);

    private void NavigateSelectedWebSite()
    {
        var site = CurrentWebSite;
        if (_webInitialized && string.Equals(_loadedSiteId, site.Id, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            WebView.Source = site.HomeUrl;
            _webInitialized = true;
            _loadedSiteId = site.Id;
            SetStatus($"已加载 {site.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"无法打开网页：{ex.Message}", isError: true);
        }
    }

    private void OnWebNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
        => _webReady = false;

    private void OnWebNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        _webReady = e.IsSuccess;
        _webReadyTcs?.TrySetResult(e.IsSuccess);
        _webReadyTcs = null;

        if (!e.IsSuccess)
        {
            SetStatus("网页加载失败，请检查网络或 WebView2 运行时", isError: true);
            return;
        }

        _ = InjectThinScrollbarStyleAsync();
    }

    private async Task InjectThinScrollbarStyleAsync()
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
            await WebView.InvokeScript(script);
        }
        catch
        {
            // CSP 可能阻止注入
        }
    }

    private async void OnHotkeyPressed()
    {
        var text = await TryGetClipboardTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            RequestActivateHost?.Invoke();
            return;
        }

        RequestActivateHost?.Invoke();
        await FillWebSiteAsync(text);
    }

    private async Task<string?> TryGetClipboardTextAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return null;

        try
        {
            var text = await clipboard.TryGetTextAsync();
            return text?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private async Task FillWebSiteAsync(string text)
    {
        var site = CurrentWebSite;

        if (!_webReady || WebView.Source is not { } source || !SamePage(source, site.HomeUrl))
        {
            SetStatus($"正在打开 {site.Name}…");
            WebView.Source = site.HomeUrl;
            _webInitialized = true;
            _loadedSiteId = site.Id;
            if (!await WaitWebReadyAsync(TimeSpan.FromSeconds(25)))
            {
                SetStatus("网页加载超时，请稍后重试", isWarning: true);
                return;
            }
        }

        Exception? lastError = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                var resultJson = await WebView.InvokeScript(WebFillAutomation.BuildFillScript(site.Id, text));
                if (TryParseFillResult(resultJson, out var ok, out var detail))
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

        SetStatus(
            $"未能自动填入 {site.Name}（{lastError?.Message ?? "未知原因"}），可手动 Ctrl+V 粘贴",
            isWarning: true);
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

    private static bool SamePage(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port
        && string.Equals(a.AbsolutePath, b.AbsolutePath, StringComparison.OrdinalIgnoreCase);

    private async Task<bool> WaitWebReadyAsync(TimeSpan timeout)
    {
        if (_webReady)
            return true;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _webReadyTcs = tcs;

        if (_webReady)
        {
            _webReadyTcs = null;
            return true;
        }

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        return ReferenceEquals(winner, tcs.Task) && tcs.Task.Result;
    }

    // 内嵌于 DevBox 时不再显示底部状态条；保留调用点便于排查流程。
    private static void SetStatus(string message, bool isWarning = false, bool isError = false) { }
}
