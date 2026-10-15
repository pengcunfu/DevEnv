using System.Text.Json;
using DevBox.Api;
using LavaTranslator.Models;
using LavaTranslator.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using FormsClipboard = System.Windows.Forms.Clipboard;

namespace DevBox.Host;

/// <summary>
/// 熔岩翻译独立窗口：自有 WebView2（不嵌在主壳 React 里），对齐独立 Translator 窗口语义。
/// </summary>
internal sealed class TranslatorHostController : IDisposable
{
    private readonly Form _owner;
    private readonly Form _window;
    private readonly Panel _toolbar = new()
    {
        Dock = DockStyle.Top,
        Height = 40,
        BackColor = Color.White,
        Padding = new Padding(8, 6, 8, 6),
    };
    private readonly ComboBox _siteCombo = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 140,
        FlatStyle = FlatStyle.System,
    };
    private readonly Button _fillBtn = new() { Text = "填充剪贴板", Width = 96, Height = 26, FlatStyle = FlatStyle.System };
    private readonly Label _status = new()
    {
        AutoSize = true,
        ForeColor = Color.FromArgb(107, 114, 128),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 4, 0, 0),
    };
    private WebView2 _webView = new() { Dock = DockStyle.Fill };

    private readonly ConfigService _config = new();
    private CoreWebView2Environment? _env;
    private bool _ready;
    private TaskCompletionSource<bool>? _readyTcs;
    private string? _loadedSiteId;
    private bool _initialized;
    private bool _filling;
    private bool _suppressSiteChange;
    /// <summary>为 true 时允许真正关闭（进程退出）；否则点关闭只隐藏。</summary>
    private bool _allowClose;

    public bool IsVisible => _window.Visible;

    public TranslatorHostController(Form owner)
    {
        _owner = owner;

        _window = new Form
        {
            Text = "熔岩翻译",
            Width = 980,
            Height = 640,
            MinimumSize = new Size(760, 480),
            StartPosition = FormStartPosition.CenterScreen,
            ShowInTaskbar = true,
            FormBorderStyle = FormBorderStyle.Sizable,
            BackColor = Color.White,
        };
        if (owner.Icon is not null)
            _window.Icon = owner.Icon;

        _window.FormClosing += (_, e) =>
        {
            // 点关闭只隐藏；进程退出须先 PrepareForExit，否则 Cancel 会连带卡住主窗退出
            if (_allowClose)
                return;
            e.Cancel = true;
            Hide();
        };

        _fillBtn.Click += async (_, _) =>
        {
            var text = TryGetClipboardText();
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("剪贴板无文本", warning: true);
                return;
            }
            await FillAsync(text);
        };
        _siteCombo.SelectedIndexChanged += async (_, _) =>
        {
            if (_suppressSiteChange)
                return;
            if (_siteCombo.SelectedItem is SiteItem item)
                await SwitchSiteAsync(item.Id, fillText: null);
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
        };
        flow.Controls.Add(_siteCombo);
        flow.Controls.Add(_fillBtn);
        flow.Controls.Add(_status);
        _toolbar.Controls.Add(flow);

        _window.Controls.Add(_webView);
        _window.Controls.Add(_toolbar);

        foreach (var site in WebTranslateCatalog.All)
            _siteCombo.Items.Add(new SiteItem(site.Id, site.Name));
    }

    public void AttachEnvironment(CoreWebView2Environment env) => _env = env;

    public async Task EnsureInitializedAsync(CoreWebView2Environment? env = null)
    {
        if (env is not null)
            _env = env;
        if (_initialized)
            return;

        // 与主壳共用 Environment / 用户数据目录，复用 Chromium 浏览器进程
        _env ??= await WebView2EnvironmentFactory.GetSharedAsync();
        await _webView.EnsureCoreWebView2Async(_env);

        WebView2Ui.ConfigureShell(
            _webView,
#if DEBUG
            enableDevTools: true,
#else
            enableDevTools: false,
#endif
            customNativeMenu: true,
            allowExternalDrop: false);
        _webView.CoreWebView2.NavigationStarting += (_, _) => _ready = false;
        _webView.CoreWebView2.NavigationCompleted += async (_, e) =>
        {
            _ready = e.IsSuccess;
            _readyTcs?.TrySetResult(e.IsSuccess);
            _readyTcs = null;
            if (e.IsSuccess)
                await InjectThinScrollbarAsync();
            else
                SetStatus("网页加载失败，请检查网络或 WebView2 运行时", error: true);
        };
        _initialized = true;

        var site = CurrentSite();
        SelectCombo(site.Id);
        NavigateHome(site);
    }

    public void Show()
    {
        if (!_window.Visible)
            _window.Show(_owner);
        if (_window.WindowState == FormWindowState.Minimized)
            _window.WindowState = FormWindowState.Normal;
        _window.BringToFront();
        _window.Activate();
    }

    public void Hide()
    {
        if (_window.Visible)
            _window.Hide();
        // 关窗即销毁控件：比 about:blank 更能拆掉 renderer（翻译站很重）
        TearDownWebView();
    }

    private void TearDownWebView()
    {
        if (!_initialized)
            return;
        _ready = false;
        _loadedSiteId = null;
        _readyTcs = null;
        _initialized = false;
        _webView = WebView2Recycle.Replace(_window, _webView);
    }

    /// <summary>Alt+Space：显示窗口；有剪贴板文本则 DOM 填充。</summary>
    public async Task HandleHotkeyAsync()
    {
        await EnsureReadyAsync();
        Show();
        var text = TryGetClipboardText();
        if (string.IsNullOrWhiteSpace(text))
        {
            var site = CurrentSite();
            if (!IsSameLoaded(site))
                NavigateHome(site);
            SetStatus($"已打开 {site.Name}（Alt+Space）");
            return;
        }

        await FillAsync(text);
    }

    public async Task ApplyAsync(TranslatorRequest req)
    {
        await EnsureReadyAsync();

        if (!req.Visible)
        {
            Hide();
            return;
        }

        Show();
        if (!string.IsNullOrWhiteSpace(req.SiteId))
        {
            var general = _config.Current.General;
            general.LastWebSiteId = req.SiteId;
            _config.UpdateGeneral(general);
            SelectCombo(req.SiteId);
        }

        var site = CurrentSite();
        var text = req.Text;
        if (req.FillClipboard && string.IsNullOrWhiteSpace(text))
            text = TryGetClipboardText();

        if (!string.IsNullOrWhiteSpace(text))
        {
            await FillAsync(text!);
            return;
        }

        if (!string.IsNullOrWhiteSpace(req.Url)
            && Uri.TryCreate(req.Url, UriKind.Absolute, out var uri)
            && !IsSamePage(uri, site.HomeUrl))
        {
            if (string.Equals(uri.Host, site.HomeUrl.Host, StringComparison.OrdinalIgnoreCase))
            {
                _ready = false;
                _webView.CoreWebView2?.Navigate(uri.ToString());
                _loadedSiteId = site.Id;
                SetStatus($"已打开 {site.Name}");
                return;
            }
        }

        if (!IsSameLoaded(site))
            NavigateHome(site);
        else
            SetStatus($"已打开 {site.Name}");
    }

    public async Task SwitchSiteAsync(string siteId, string? fillText)
    {
        await EnsureReadyAsync();
        var site = WebTranslateCatalog.FindById(siteId) ?? WebTranslateCatalog.All[0];
        var general = _config.Current.General;
        general.LastWebSiteId = site.Id;
        _config.UpdateGeneral(general);
        SelectCombo(site.Id);

        if (!string.IsNullOrWhiteSpace(fillText))
        {
            await FillAsync(fillText);
            return;
        }

        NavigateHome(site);
    }

    private async Task EnsureReadyAsync()
    {
        if (_initialized)
            return;
        await EnsureInitializedAsync(_env);
    }

    private async Task FillAsync(string text)
    {
        if (_filling)
            return;
        _filling = true;
        try
        {
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
                    if (_webView.CoreWebView2 is null)
                        break;
                    var resultJson = await _webView.CoreWebView2.ExecuteScriptAsync(
                        WebFillAutomation.BuildFillScript(site.Id, text));
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
                warning: true);
        }
        finally
        {
            _filling = false;
        }
    }

    private void NavigateHome(WebTranslateSite site)
    {
        if (_webView.CoreWebView2 is null)
            return;
        _ready = false;
        _webView.CoreWebView2.Navigate(site.HomeUrl.ToString());
        _loadedSiteId = site.Id;
        SetStatus($"已加载 {site.Name}");
    }

    private WebTranslateSite CurrentSite() =>
        WebTranslateCatalog.FindById(_config.Current.General.LastWebSiteId)
        ?? WebTranslateCatalog.All[0];

    private bool IsSameLoaded(WebTranslateSite site) =>
        string.Equals(_loadedSiteId, site.Id, StringComparison.OrdinalIgnoreCase) && _ready;

    private void SelectCombo(string siteId)
    {
        for (var i = 0; i < _siteCombo.Items.Count; i++)
        {
            if (_siteCombo.Items[i] is SiteItem s && string.Equals(s.Id, siteId, StringComparison.OrdinalIgnoreCase))
            {
                if (_siteCombo.SelectedIndex != i)
                {
                    _suppressSiteChange = true;
                    try { _siteCombo.SelectedIndex = i; }
                    finally { _suppressSiteChange = false; }
                }
                return;
            }
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
            if (_webView.CoreWebView2 is not null)
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch
        {
            // CSP
        }
    }

    private void SetStatus(string message, bool warning = false, bool error = false)
    {
        void Apply()
        {
            _status.Text = message;
            _status.ForeColor = error
                ? Color.FromArgb(185, 28, 28)
                : warning
                    ? Color.FromArgb(180, 83, 9)
                    : Color.FromArgb(107, 114, 128);
        }

        if (_window.IsHandleCreated && _window.InvokeRequired)
            _window.BeginInvoke(Apply);
        else if (_owner.IsHandleCreated && _owner.InvokeRequired)
            _owner.BeginInvoke(Apply);
        else
            Apply();
    }

    private static string? TryGetClipboardText()
    {
        try
        {
            if (!FormsClipboard.ContainsText())
                return null;
            return FormsClipboard.GetText()?.Trim();
        }
        catch
        {
            return null;
        }
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

    private static bool IsSamePage(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port
        && string.Equals(a.AbsolutePath, b.AbsolutePath, StringComparison.OrdinalIgnoreCase);

    /// <summary>允许窗口真正关闭。主窗 Exit 前必须调用，否则 owned form 的 Cancel 会阻止主窗关闭。</summary>
    public void PrepareForExit() => _allowClose = true;

    public void Dispose()
    {
        _allowClose = true;
        try
        {
            if (!_webView.IsDisposed)
                _webView.Dispose();
            _window.Close();
            _window.Dispose();
        }
        catch
        {
            // ignore
        }
    }

    private sealed record SiteItem(string Id, string Name)
    {
        public override string ToString() => Name;
    }
}
