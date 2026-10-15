using System.Diagnostics;
using System.Drawing.Imaging;
using System.Net.Http.Json;
using ClipboardApp.Models;
using DevBox.Api;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using FormsClipboard = System.Windows.Forms.Clipboard;

namespace DevBox.Host;

public sealed class MainForm : Form
{
    private readonly DevBoxApiHost _api;
    private readonly Panel _content = new() { Dock = DockStyle.Fill };
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly TranslatorHostController _translator;
    private readonly ClipboardHostController _clipboardWindow;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _clipboardTimer = new() { Interval = 400 };
    private GlobalHotkeys? _hotkeys;
    private bool _exiting;
    private string? _lastClipboardSignature;

    public MainForm(DevBoxApiHost api)
    {
        _api = api;
        _translator = new TranslatorHostController(this);
        _clipboardWindow = new ClipboardHostController(this, api.SessionToken);

        Text = "程序盒子";
        Width = 1100;
        Height = 700;
        MinimumSize = new Size(900, 560);
        StartPosition = FormStartPosition.CenterScreen;
        // 系统默认标题栏 / 边框（最小化、最大化、关闭）
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        BackColor = Color.FromArgb(243, 244, 246);

        _content.Controls.Add(_webView);
        Controls.Add(_content);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");
        Icon? appIcon = null;
        try
        {
            if (File.Exists(iconPath))
                appIcon = new Icon(iconPath);
        }
        catch { /* ignore */ }

        if (appIcon is not null)
            Icon = appIcon;

        _tray = new NotifyIcon
        {
            Text = "程序盒子",
            Visible = true,
            Icon = appIcon ?? SystemIcons.Application,
        };
        _tray.DoubleClick += (_, _) => ShowFromTray();
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => ShowFromTray());
        menu.Items.Add("退出", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;

        HostCallbacks.PickExecutable = () =>
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "可执行文件 (*.exe)|*.exe",
                Multiselect = false,
            };
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        };
        HostCallbacks.PickFolder = () =>
        {
            using var dlg = new FolderBrowserDialog();
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.SelectedPath : null;
        };
        HostCallbacks.PickFiles = filter =>
        {
            using var dlg = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = true,
            };
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileNames : null;
        };
        HostCallbacks.OnTrayIconChanged = visible =>
        {
            BeginInvoke(() => _tray.Visible = visible);
        };
        HostCallbacks.OnActivateTab = tab =>
        {
            BeginInvoke(async () =>
            {
                ShowFromTray();
                if (string.Equals(tab, "translator", StringComparison.OrdinalIgnoreCase))
                {
                    await ShowTranslatorWindowAsync(new TranslatorRequest { Visible = true });
                    if (_webView.CoreWebView2 is not null)
                        _ = _webView.CoreWebView2.ExecuteScriptAsync(
                            "window.location.hash = '#/translator'; window.dispatchEvent(new Event('devbox-activate-tab'));");
                    return;
                }

                if (string.Equals(tab, "clipboard", StringComparison.OrdinalIgnoreCase))
                {
                    await ShowClipboardWindowAsync(visible: true);
                    if (_webView.CoreWebView2 is not null)
                        _ = _webView.CoreWebView2.ExecuteScriptAsync(
                            "window.location.hash = '#/clipboard'; window.dispatchEvent(new Event('devbox-activate-tab'));");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(tab) && _webView.CoreWebView2 is not null)
                    _ = _webView.CoreWebView2.ExecuteScriptAsync(
                        $"window.location.hash = '#/{tab}'; window.dispatchEvent(new Event('devbox-activate-tab'));");
            });
        };
        HostCallbacks.OnShowTranslator = req =>
        {
            BeginInvoke(async () => await ShowTranslatorWindowAsync(req));
        };
        HostCallbacks.OnShowClipboard = visible =>
        {
            BeginInvoke(async () => await ShowClipboardWindowAsync(visible));
        };
        HostCallbacks.OnExitApp = () => BeginInvoke(ExitApp);
        HostCallbacks.OnBeforeExitForUpdate = () =>
        {
            _exiting = true;
            _clipboardTimer.Stop();
            _tray.Visible = false;
        };
        HostCallbacks.OnShowWindowHotkeyChanged = hotkey =>
        {
            BeginInvoke(() => _hotkeys?.ApplyShowWindowHotkey(hotkey));
        };
        HostCallbacks.SaveFile = (suggested, filter) =>
        {
            using var dlg = new SaveFileDialog
            {
                FileName = suggested,
                Filter = filter,
            };
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        };
        HostCallbacks.SetClipboardText = text =>
        {
            BeginInvoke(() =>
            {
                try { FormsClipboard.SetText(text); }
                catch { /* busy */ }
            });
        };
        HostCallbacks.SetClipboardImagePath = path =>
        {
            BeginInvoke(() =>
            {
                try
                {
                    using var img = Image.FromFile(path);
                    FormsClipboard.SetImage(img);
                }
                catch { /* ignore */ }
            });
        };
        HostCallbacks.RestartElevated = asAdmin =>
        {
            BeginInvoke(() => RestartProcess(asAdmin));
        };

        FormClosing += OnFormClosing;
        // 句柄创建/重建时都要（重新）注册；WebView2 等可能触发 RecreateHandle
        HandleCreated += (_, _) => EnsureHotkeys(forceRebind: true);
        Shown += (_, _) => EnsureHotkeys(forceRebind: false);
        Load += async (_, _) => await InitializeWebViewsAsync();

        _clipboardTimer.Tick += (_, _) => PollClipboard();
        _clipboardTimer.Start();

        // 标题栏等非 WebView 区域仍可拖入
        AllowDrop = true;
        DragEnter += OnFormDragEnter;
        DragOver += OnFormDragEnter;
        DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
                await HandleDroppedPathsAsync(files);
        };
    }

    private static void OnFormDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private Task HandleDroppedPathsAsync(IReadOnlyList<string> files)
    {
        if (files.Count == 0)
            return Task.CompletedTask;

        // WebMessage / Navigation 回调可能在非 UI 线程
        if (InvokeRequired)
        {
            BeginInvoke(() => _ = HandleDroppedPathsOnUiAsync(files));
            return Task.CompletedTask;
        }

        return HandleDroppedPathsOnUiAsync(files);
    }

    private async Task HandleDroppedPathsOnUiAsync(IReadOnlyList<string> files)
    {
        var addedApps = 0;
        var addedFolders = 0;
        try
        {
            using var client = CreateApiClient();
            foreach (var file in files)
            {
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(file))
                {
                    var resp = await client.PostAsJsonAsync("/api/native-apps", new { path = file });
                    if (resp.IsSuccessStatusCode)
                        addedApps++;
                }
                else if (Directory.Exists(file))
                {
                    var resp = await client.PostAsJsonAsync("/api/folders", new { path = file });
                    if (resp.IsSuccessStatusCode)
                        addedFolders++;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"添加失败：{ex.Message}", "程序盒子", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var added = addedApps + addedFolders;
        if (added == 0)
            return;

        if (_webView.CoreWebView2 is not null)
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(
                "window.dispatchEvent(new Event('devbox-shortcuts-changed'));");
            var tab = addedFolders > 0 && addedApps == 0 ? "folders" : "apps";
            await _webView.CoreWebView2.ExecuteScriptAsync(
                $"window.location.hash = '#/{tab}'; window.dispatchEvent(new Event('devbox-activate-tab'));");
        }

        _tray.ShowBalloonTip(2000, "程序盒子", $"已添加 {added} 项", ToolTipIcon.Info);
    }

    private HttpClient CreateApiClient()
    {
        var client = new HttpClient { BaseAddress = new Uri(ApiConstants.ApiBaseUrl) };
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _api.SessionToken);
        return client;
    }

    private async Task InitializeWebViewsAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync();
            await _webView.EnsureCoreWebView2Async(env);
            await _translator.EnsureInitializedAsync(env);
            await _clipboardWindow.EnsureInitializedAsync(env);
            // 主壳：无默认菜单/状态栏；允许外部拖入，由 DropBridge 取绝对路径后调 API
            WebView2Ui.ConfigureShell(
                _webView,
                enableDevTools: true,
                customNativeMenu: false,
                allowExternalDrop: true);
            await WebViewDropBridge.AttachAsync(_webView, HandleDroppedPathsAsync);

            var uiUrl = $"{ApiConstants.UiBaseUrl}/?token={Uri.EscapeDataString(_api.SessionToken)}";
            var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");

            if (await IsUiDevServerUpAsync())
                _webView.CoreWebView2.Navigate(uiUrl);
            else if (File.Exists(wwwroot))
                _webView.CoreWebView2.Navigate($"http://{ApiConstants.Loopback}:{ApiConstants.ApiPort}/?token={Uri.EscapeDataString(_api.SessionToken)}");
            else
                ShowBootstrapPage();

            // WebView2 初始化后可能重建窗口句柄，补注册热键
            EnsureHotkeys(forceRebind: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"WebView2 初始化失败。请安装 Microsoft Edge WebView2 Runtime。\n\n{ex.Message}",
                "程序盒子",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static async Task<bool> IsUiDevServerUpAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(600) };
            using var resp = await client.GetAsync(ApiConstants.UiBaseUrl);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void ShowBootstrapPage()
    {
        var html = $$"""
            <!DOCTYPE html><html lang="zh-CN"><head><meta charset="utf-8"/><title>程序盒子</title>
            <style>body{font-family:Segoe UI,sans-serif;background:#f3f4f6;display:flex;align-items:center;justify-content:center;height:100vh;margin:0}
            .card{background:#fff;border:1px solid #e5e7eb;border-radius:12px;padding:28px 32px;max-width:520px}
            code{background:#f3f4f6;padding:2px 6px;border-radius:4px}</style></head>
            <body><div class="card"><h1>程序盒子 · 新壳已启动</h1>
            <p>API：<code>{{ApiConstants.ApiBaseUrl}}</code></p>
            <p>请运行 <code>make ui</code> 启动 HMR（:35173），然后刷新。</p></div></body></html>
            """;
        _webView.CoreWebView2.NavigateToString(html);
    }

    private void PollClipboard()
    {
        try
        {
            if (FormsClipboard.ContainsText())
            {
                var text = FormsClipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                    return;
                var signature = "text:" + text;
                if (signature == _lastClipboardSignature)
                    return;
                _lastClipboardSignature = signature;
                var entry = new ClipboardEntry
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Type = "text",
                    Text = text,
                };
                ClipboardEndpoints.SharedHistory.Insert(entry);
            }
            else if (FormsClipboard.ContainsImage())
            {
                using var image = FormsClipboard.GetImage();
                if (image is null)
                    return;
                var signature = $"img:{image.Width}x{image.Height}";
                if (signature == _lastClipboardSignature)
                    return;
                _lastClipboardSignature = signature;
                var dir = ClipboardApp.Services.AppPaths.ImagesDir;
                Directory.CreateDirectory(dir);
                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                var path = Path.Combine(dir, fileName);
                image.Save(path, ImageFormat.Png);
                var entry = new ClipboardEntry
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Type = "image",
                    ImagePath = fileName,
                };
                ClipboardEndpoints.SharedHistory.Insert(entry);
            }
        }
        catch
        {
            // clipboard busy
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_exiting)
            return;
        e.Cancel = true;
        CloseToTray();
    }

    private void CloseToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void EnsureHotkeys(bool forceRebind)
    {
        if (!IsHandleCreated || Handle == IntPtr.Zero)
            return;

        if (_hotkeys is null)
        {
            _hotkeys = new GlobalHotkeys(Handle);
            _hotkeys.ShowWindowPressed += () => BeginInvoke(ShowFromTray);
            _hotkeys.TranslatorPressed += () => BeginInvoke(async () =>
            {
                await ShowTranslatorWindowAsync(new TranslatorRequest
                {
                    Visible = true,
                    FillClipboard = true,
                });
            });
            _hotkeys.ClipboardPressed += () => BeginInvoke(async () =>
            {
                // Ctrl+Space：显示/前置独立剪贴板窗口（已显示则再次前置并刷新）
                await ShowClipboardWindowAsync(visible: true);
            });
        }
        else if (forceRebind)
        {
            _hotkeys.Rebind(Handle);
        }
        else if (_hotkeys.TranslatorRegistered && _hotkeys.ClipboardRegistered)
        {
            return;
        }

        try
        {
            var store = new ConfigStore();
            var cfg = store.GetConfig();
            var showHotkey = cfg.Value<string>("ShowWindowHotkey") ?? "Ctrl+Alt+D";
            _hotkeys.ApplyShowWindowHotkey(showHotkey);
        }
        catch
        {
            _hotkeys.ApplyShowWindowHotkey("Ctrl+Alt+D");
        }

        var altOk = _hotkeys.RegisterTranslatorAltSpace();
        var ctrlOk = _hotkeys.RegisterClipboardCtrlSpace();
        if (!altOk || !ctrlOk)
        {
            var parts = new List<string>();
            if (!altOk) parts.Add("Alt+Space（熔岩翻译）");
            if (!ctrlOk) parts.Add("Ctrl+Space（熔岩剪贴板）");
            // 常见原因：旧版 DevBox / 其它程序已占用，或输入法占用 Ctrl+Space
            BeginInvoke(() =>
            {
                if (IsDisposed) return;
                _tray.ShowBalloonTip(
                    4000,
                    "程序盒子",
                    $"快捷键注册失败：{string.Join("、", parts)}。请关闭其它占用程序后重启。",
                    ToolTipIcon.Warning);
            });
        }
    }

    private async Task ShowTranslatorWindowAsync(TranslatorRequest req)
    {
        await _translator.ApplyAsync(req);
    }

    private async Task ShowClipboardWindowAsync(bool visible)
    {
        await _clipboardWindow.ApplyAsync(visible);
    }

    private void RestartProcess(bool asAdmin)
    {
        var processPath = Environment.ProcessPath;
        var assemblyPath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        string fileName;
        string arguments;
        if (processPath is not null
            && Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(assemblyPath))
        {
            fileName = processPath;
            arguments = $"\"{assemblyPath}\"";
        }
        else
        {
            fileName = processPath ?? assemblyPath ?? string.Empty;
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
                // explorer demote: relaunch without admin token
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = string.IsNullOrEmpty(arguments)
                        ? $"\"{fileName}\""
                        : $"\"{fileName}\" {arguments}",
                    UseShellExecute = true,
                });
            }

            ExitApp();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // UAC cancelled
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"无法重启: {ex.Message}", "程序盒子", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExitApp()
    {
        _exiting = true;
        _clipboardTimer.Stop();
        _hotkeys?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        Close();
        Application.Exit();
    }

    protected override void WndProc(ref Message m)
    {
        if (_hotkeys?.TryHandle(ref m) == true)
            return;
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeys?.Dispose();
            _clipboardTimer.Dispose();
            _tray.Dispose();
            _webView.Dispose();
            _translator.Dispose();
            _clipboardWindow.Dispose();
        }
        base.Dispose(disposing);
    }
}
