using DevBox.Api;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DevBox.Host;

/// <summary>
/// 熔岩剪贴板独立窗口：自有 WebView2 加载 React 剪贴板页（无主壳侧栏）。
/// </summary>
internal sealed class ClipboardHostController : IDisposable
{
    private readonly Form _owner;
    private readonly Form _window;
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly string _sessionToken;

    private CoreWebView2Environment? _env;
    private bool _initialized;
    private bool _closingToHide;
    private bool _navigated;

    public bool IsVisible => _window.Visible;

    public ClipboardHostController(Form owner, string sessionToken)
    {
        _owner = owner;
        _sessionToken = sessionToken;

        _window = new Form
        {
            Text = "熔岩剪贴板",
            Width = 920,
            Height = 640,
            MinimumSize = new Size(720, 480),
            StartPosition = FormStartPosition.CenterScreen,
            ShowInTaskbar = true,
            FormBorderStyle = FormBorderStyle.Sizable,
            BackColor = Color.White,
        };
        if (owner.Icon is not null)
            _window.Icon = owner.Icon;

        _window.FormClosing += (_, e) =>
        {
            if (_closingToHide)
                return;
            e.Cancel = true;
            Hide();
        };

        _window.Controls.Add(_webView);
    }

    public async Task EnsureInitializedAsync(CoreWebView2Environment env)
    {
        _env = env;
        if (_initialized)
            return;

        var profile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevBox",
            "WebView2-Clipboard");
        Directory.CreateDirectory(profile);
        var clipEnv = await CoreWebView2Environment.CreateAsync(null, profile);
        await _webView.EnsureCoreWebView2Async(clipEnv);

        WebView2Ui.ConfigureShell(
            _webView,
            enableDevTools: true,
            customNativeMenu: false,
            allowExternalDrop: false);

        _initialized = true;
    }

    public async Task ShowAsync()
    {
        await EnsureReadyAsync();
        if (!_navigated && _webView.CoreWebView2 is not null)
        {
            var url = await ResolveUiUrlAsync();
            _webView.CoreWebView2.Navigate(url);
            _navigated = true;
        }

        if (!_window.Visible)
            _window.Show(_owner);
        if (_window.WindowState == FormWindowState.Minimized)
            _window.WindowState = FormWindowState.Normal;
        _window.BringToFront();
        _window.Activate();

        // 每次唤起刷新列表
        if (_webView.CoreWebView2 is not null)
        {
            _ = _webView.CoreWebView2.ExecuteScriptAsync(
                "window.dispatchEvent(new Event('devbox-clipboard-refresh'));");
        }
    }

    public void Hide()
    {
        if (_window.Visible)
            _window.Hide();
    }

    public async Task ApplyAsync(bool visible)
    {
        if (!visible)
        {
            Hide();
            return;
        }

        await ShowAsync();
    }

    private async Task EnsureReadyAsync()
    {
        if (_initialized)
            return;
        if (_env is not null)
            await EnsureInitializedAsync(_env);
        else
            await EnsureInitializedAsync(await CoreWebView2Environment.CreateAsync());
    }

    private async Task<string> ResolveUiUrlAsync()
    {
        var token = Uri.EscapeDataString(_sessionToken);
        var hash = "#/clipboard-window";
        if (await IsUiDevServerUpAsync())
            return $"{ApiConstants.UiBaseUrl}/?token={token}{hash}";

        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        if (File.Exists(wwwroot))
            return $"http://{ApiConstants.Loopback}:{ApiConstants.ApiPort}/?token={token}{hash}";

        // 无 UI 时仍打开窗口，显示简单提示页
        return "about:blank";
    }

    private static async Task<bool> IsUiDevServerUpAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(400) };
            using var resp = await client.GetAsync(ApiConstants.UiBaseUrl);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _closingToHide = true;
        try
        {
            _webView.Dispose();
            _window.Close();
            _window.Dispose();
        }
        catch
        {
            // ignore
        }
    }
}
