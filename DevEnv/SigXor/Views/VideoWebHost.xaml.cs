using System.IO;
using System.Text;
using Microsoft.Web.WebView2.Core;
using DevEnv.Core.SigXor;
using DevEnv.Host.SigXor.ViewModels;

namespace DevEnv.Host.SigXor.Views;

/// <summary>
/// 视频播放宿主：播放页与视频字节均经独立未映射域名 + WebResourceRequested 回吐（含 Range）。
/// </summary>
public partial class VideoWebHost : WebViewHostBase
{
    public const string VideoDataHost = "see-video.local";

    private string? _path;
    private string? _name;
    private long _size;

    public VideoWebHost()
    {
        InitializeComponent();
    }

    public Task LoadAsync(string path, string name, long size)
    {
        _path = path;
        _name = name;
        _size = size;
        NavigateOrPending(BuildPlayerUrl());
        return Task.CompletedTask;
    }

    public override void PrepareForReuse()
    {
        base.PrepareForReuse();
        _path = null;
        _name = null;
        _size = 0;
    }

    protected override void Configure(CoreWebView2 core)
    {
        core.WebResourceRequested += OnWebResourceRequested;
        core.AddWebResourceRequestedFilter($"https://{VideoDataHost}/*", CoreWebView2WebResourceContext.All);
        core.WebMessageReceived += OnWebMessageReceived;

        if (_path is not null)
            NavigateOrPending(BuildPlayerUrl());
    }

    private string BuildPlayerUrl()
    {
        var sizeText = FileEntry.FormatSize(_size);
        return WithCacheBust(
            $"https://{VideoDataHost}/video-player.html"
            + $"?name={Uri.EscapeDataString(_name ?? "")}&size={Uri.EscapeDataString(sizeText)}");
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)
            || !uri.Host.Equals(VideoDataHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string path = uri.AbsolutePath;
        if (path.Equals("/video-player.html", StringComparison.OrdinalIgnoreCase))
        {
            ServePlayerPage(e);
            return;
        }

        if (path.Equals("/stream", StringComparison.OrdinalIgnoreCase))
        {
            ServeVideoStream(e);
            return;
        }

        e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Not Found", "");
    }

    private void ServePlayerPage(CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var htmlPath = WebAssets.GetPath("video-player.html");
            var stream = File.OpenRead(htmlPath);
            e.Response = SharedEnvironment.CreateWebResourceResponse(
                stream, 200, "OK", "Content-Type: text/html; charset=utf-8\nCache-Control: no-cache");
        }
        catch (Exception ex)
        {
            var bytes = Encoding.UTF8.GetBytes($"<!DOCTYPE html><pre>播放页加载失败：{ex.Message}</pre>");
            e.Response = SharedEnvironment.CreateWebResourceResponse(
                new MemoryStream(bytes), 500, "Error", "Content-Type: text/html; charset=utf-8");
        }
    }

    private void ServeVideoStream(CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_path is null)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 404, "Not Found", "");
            return;
        }

        try
        {
            string mime = MimeFor(_path);
            string? rangeHeader = null;
            if (e.Request.Headers.Contains("Range"))
                rangeHeader = e.Request.Headers.GetHeader("Range");

            long total = new FileInfo(_path).Length;
            var spec = RangeSpec.Parse(rangeHeader, total);

            if (spec is { } s)
            {
                var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                stream.Position = s.Start;
                long length = s.End - s.Start + 1;
                var body = new SubStream(stream, length);
                string headers =
                    $"Content-Type: {mime}\nContent-Length: {length}\nAccept-Ranges: bytes\n" +
                    $"Content-Range: bytes {s.Start}-{s.End}/{total}";
                e.Response = SharedEnvironment.CreateWebResourceResponse(body, 206, "Partial Content", headers);
            }
            else
            {
                var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                e.Response = SharedEnvironment.CreateWebResourceResponse(
                    stream, 200, "OK",
                    $"Content-Type: {mime}\nContent-Length: {total}\nAccept-Ranges: bytes");
            }
        }
        catch (Exception ex)
        {
            e.Response = SharedEnvironment.CreateWebResourceResponse(null, 500, ex.Message, "");
            if (DataContext is VideoContentViewModel vm)
                Dispatcher.Invoke(() => vm.Error = $"视频读取失败：{ex.Message}");
        }
    }

    private static string MimeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" => "video/mp4",
        ".webm" => "video/webm",
        ".ogv" => "video/ogg",
        ".mov" => "video/quicktime",
        ".mkv" => "video/x-matroska",
        ".avi" => "video/x-msvideo",
        ".wmv" => "video/x-ms-wmv",
        ".mpg" or ".mpeg" => "video/mpeg",
        _ => "application/octet-stream",
    };

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var vm = DataContext as VideoContentViewModel;
        if (vm is null) return;
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (json?.Contains("\"type\":\"error\"") == true)
                Dispatcher.Invoke(() => vm.Error = ExtractMessage(json));
        }
        catch
        {
            // 非字符串消息忽略
        }
    }

    private static string ExtractMessage(string json)
    {
        int idx = json.IndexOf("\"message\":\"", StringComparison.Ordinal);
        if (idx < 0) return json;
        int start = idx + 11;
        int end = json.IndexOf('"', start);
        return end > start ? json[start..end] : json;
    }

    protected override void OnDetach(CoreWebView2 core)
    {
        core.WebResourceRequested -= OnWebResourceRequested;
        core.WebMessageReceived -= OnWebMessageReceived;
    }

    private sealed class SubStream : Stream
    {
        private readonly Stream _underlying;
        private readonly long _length;
        private long _remaining;

        public SubStream(Stream underlying, long length)
        {
            _underlying = underlying;
            _length = length;
            _remaining = length;
        }

        public override bool CanRead => _underlying.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _length - _remaining;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0) return 0;
            int toRead = (int)Math.Min(count, _remaining);
            int read = _underlying.Read(buffer, offset, toRead);
            _remaining -= read;
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _underlying.Dispose();
            base.Dispose(disposing);
        }
    }
}
