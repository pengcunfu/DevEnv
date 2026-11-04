using System.Windows.Threading;

namespace DevEnv.Host.SigXor.Views;

/// <summary>
/// WebView2 宿主常驻池：启动只预热共享 Environment；各类型宿主首次使用时创建并复用，
/// 关闭预览时归还不销毁 Core，避免每次空格都重新 EnsureCoreWebView2。
/// </summary>
public static class WebViewHostPool
{
    private static readonly object Gate = new();
    private static bool _warmStarted;
    private static bool _disposed;

    private static MarkdownWebHost? _markdown;
    private static PdfWebHost? _pdf;
    private static OfficeWebHost? _office;
    private static HtmlWebHost? _html;
    private static AudioWebHost? _audio;
    private static VideoWebHost? _video;
    private static SvgWebHost? _svg;

    private static bool _markdownInUse;
    private static bool _pdfInUse;
    private static bool _officeInUse;
    private static bool _htmlInUse;
    private static bool _audioInUse;
    private static bool _videoInUse;
    private static bool _svgInUse;

    /// <summary>
    /// 仅预热共享 WebView2 Environment（轻量）。
    /// 不在启动时拉起多个浏览器 Core，以免抢 CPU/内存拖慢首预览。
    /// </summary>
    public static void StartWarmup(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        lock (Gate)
        {
            if (_disposed || _warmStarted) return;
            _warmStarted = true;
        }

        _ = WarmEnvironmentOnlyAsync(dispatcher);
    }

    private static async Task WarmEnvironmentOnlyAsync(Dispatcher dispatcher)
    {
        if (!WebViewHostBase.IsRuntimeAvailable()) return;

        try
        {
            await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Background);
            await WebViewHostBase.WarmEnvironmentAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebViewHostPool environment warmup failed: {ex.Message}");
        }
    }

    public static MarkdownWebHost RentMarkdown() => Rent(ref _markdown, ref _markdownInUse, static () => new MarkdownWebHost());
    public static PdfWebHost RentPdf() => Rent(ref _pdf, ref _pdfInUse, static () => new PdfWebHost());
    public static OfficeWebHost RentOffice() => Rent(ref _office, ref _officeInUse, static () => new OfficeWebHost());
    public static HtmlWebHost RentHtml() => Rent(ref _html, ref _htmlInUse, static () => new HtmlWebHost());
    public static AudioWebHost RentAudio() => Rent(ref _audio, ref _audioInUse, static () => new AudioWebHost());
    public static VideoWebHost RentVideo() => Rent(ref _video, ref _videoInUse, static () => new VideoWebHost());
    public static SvgWebHost RentSvg() => Rent(ref _svg, ref _svgInUse, static () => new SvgWebHost());

    private static T Rent<T>(ref T? slot, ref bool inUse, Func<T> factory) where T : WebViewHostBase
    {
        lock (Gate)
        {
            if (_disposed) return factory();

            if (slot is not null && !inUse)
            {
                inUse = true;
                return slot;
            }

            // 池内实例占用中：临时实例，归还时 Dispose
            return factory();
        }
    }

    /// <summary>归还宿主：清空业务状态，保留 Core；不 NavigateBlank，避免多余整页往返。</summary>
    public static void Return(WebViewHostBase? host)
    {
        if (host is null) return;

        try
        {
            host.DataContext = null;
            host.PrepareForReuse();
        }
        catch
        {
            // 归还路径不能抛
        }

        lock (Gate)
        {
            if (ReferenceEquals(host, _markdown)) { _markdownInUse = false; return; }
            if (ReferenceEquals(host, _pdf)) { _pdfInUse = false; return; }
            if (ReferenceEquals(host, _office)) { _officeInUse = false; return; }
            if (ReferenceEquals(host, _html)) { _htmlInUse = false; return; }
            if (ReferenceEquals(host, _audio)) { _audioInUse = false; return; }
            if (ReferenceEquals(host, _video)) { _videoInUse = false; return; }
            if (ReferenceEquals(host, _svg)) { _svgInUse = false; return; }
        }

        try { host.Dispose(); } catch { /* ignore */ }
    }

    /// <summary>进程崩溃后丢弃失效宿主，下次 Rent 会新建并重新 EnsureCore。</summary>
    public static void NotifyHostCrashed(WebViewHostBase host)
    {
        lock (Gate)
        {
            if (ReferenceEquals(host, _markdown)) { _markdown = null; _markdownInUse = false; }
            else if (ReferenceEquals(host, _pdf)) { _pdf = null; _pdfInUse = false; }
            else if (ReferenceEquals(host, _office)) { _office = null; _officeInUse = false; }
            else if (ReferenceEquals(host, _html)) { _html = null; _htmlInUse = false; }
            else if (ReferenceEquals(host, _audio)) { _audio = null; _audioInUse = false; }
            else if (ReferenceEquals(host, _video)) { _video = null; _videoInUse = false; }
            else if (ReferenceEquals(host, _svg)) { _svg = null; _svgInUse = false; }
        }

        try { host.Dispose(); } catch { /* ignore */ }
    }

    public static void DisposeAll()
    {
        lock (Gate)
        {
            _disposed = true;
            DisposeSlot(ref _markdown, ref _markdownInUse);
            DisposeSlot(ref _pdf, ref _pdfInUse);
            DisposeSlot(ref _office, ref _officeInUse);
            DisposeSlot(ref _html, ref _htmlInUse);
            DisposeSlot(ref _audio, ref _audioInUse);
            DisposeSlot(ref _video, ref _videoInUse);
            DisposeSlot(ref _svg, ref _svgInUse);
        }
    }

    private static void DisposeSlot<T>(ref T? slot, ref bool inUse) where T : WebViewHostBase
    {
        try { slot?.Dispose(); } catch { /* ignore */ }
        slot = null;
        inUse = false;
    }
}
