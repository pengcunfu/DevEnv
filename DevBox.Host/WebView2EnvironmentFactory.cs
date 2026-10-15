using Microsoft.Web.WebView2.Core;

namespace DevBox.Host;

/// <summary>
/// 全进程共用一个 WebView2 Environment / 用户数据目录，便于浏览器进程复用，
/// 避免主壳 / 翻译 / 剪贴板各开一套 Chromium（进程数与内存成倍涨）。
/// </summary>
internal static class WebView2EnvironmentFactory
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static CoreWebView2Environment? _shared;
    private static string? _userDataFolder;

    /// <summary>
    /// 面向托盘工具的精简 Chromium 参数：关掉扩展/同步/后台联网等，并启用 low-end 模式压缓存。
    /// 不禁用 GPU（滚动与合成体感更差，收益不稳定）。
    /// </summary>
    private static readonly string BrowserArgs = string.Join(' ',
        "--enable-low-end-device-mode",
        "--renderer-process-limit=2",
        "--js-flags=--max-old-space-size=192",
        "--disable-extensions",
        "--disable-component-extensions-with-background-pages",
        "--disable-background-networking",
        "--disable-background-timer-throttling",
        "--disable-backgrounding-occluded-windows",
        "--disable-breakpad",
        "--disable-client-side-phishing-detection",
        "--disable-default-apps",
        "--disable-domain-reliability",
        "--disable-features=TranslateUI,AudioServiceOutOfProcess,IsolateOrigins,site-per-process",
        "--disable-hang-monitor",
        "--disable-ipc-flooding-protection",
        "--disable-popup-blocking",
        "--disable-prompt-on-repost",
        "--disable-renderer-backgrounding",
        "--disable-sync",
        "--metrics-recording-only",
        "--no-first-run",
        "--no-default-browser-check");

    public static string UserDataFolder =>
        _userDataFolder ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevBox",
            "WebView2");

    public static async Task<CoreWebView2Environment> GetSharedAsync()
    {
        if (_shared is not null)
            return _shared;

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_shared is not null)
                return _shared;

            Directory.CreateDirectory(UserDataFolder);
            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = BrowserArgs,
                AreBrowserExtensionsEnabled = false,
            };
            _shared = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: UserDataFolder,
                options: options).ConfigureAwait(false);
            return _shared;
        }
        finally
        {
            Gate.Release();
        }
    }
}
