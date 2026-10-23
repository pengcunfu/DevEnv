using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using WpfClipboard = System.Windows.Clipboard;

namespace DevBox.Host.Infrastructure;

/// <summary>一次剪贴板捕获：文本直接给字符串，图片给 PNG 字节（由上层决定落库）。</summary>
public sealed record ClipboardCapture(string Kind, string? Text, byte[]? PngBytes)
{
    /// <summary>去重签名：文本用内容，图片用 PNG 字节的哈希（比尺寸指纹可靠得多）。</summary>
    public string Signature { get; init; } = string.Empty;
}

/// <summary>
/// 剪贴板轮询（WPF 主线程即 STA，直接用 System.Windows.Clipboard，无需 WinForms 剪贴板）。
/// 400ms 一次，与历史版本节奏一致；重复内容按签名丢弃。
/// </summary>
internal sealed class ClipboardWatcher : IDisposable
{
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private string? _lastSignature;

    /// <summary>捕获到新内容时触发（已在 UI 线程）。</summary>
    public event Action<ClipboardCapture>? Captured;

    public ClipboardWatcher()
    {
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    private void Poll()
    {
        try
        {
            if (WpfClipboard.ContainsText())
            {
                var text = WpfClipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                    return;
                var signature = "text:" + text;
                if (signature == _lastSignature)
                    return;
                _lastSignature = signature;
                Captured?.Invoke(new ClipboardCapture("text", text, null) { Signature = signature });
                return;
            }

            if (WpfClipboard.ContainsImage())
            {
                var png = EncodePng(WpfClipboard.GetImage());
                if (png is null || png.Length == 0)
                    return;
                var signature = "img:" + Convert.ToHexString(SHA256.HashData(png));
                if (signature == _lastSignature)
                    return;
                _lastSignature = signature;
                Captured?.Invoke(new ClipboardCapture("image", null, png) { Signature = signature });
            }
        }
        catch
        {
            // 剪贴板被其它进程占用：下一轮重试
        }
    }

    private static byte[]? EncodePng(BitmapSource? source)
    {
        if (source is null)
            return null;
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= (_, _) => Poll();
    }
}