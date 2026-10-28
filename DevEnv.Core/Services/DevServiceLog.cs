using System.Text;

namespace DevEnv.Core.Services;

/// <summary>
/// 单个服务的运行日志：内存环形缓冲给界面实时查看，同时落盘到用户数据目录，
/// 重启程序后仍能翻到上一次运行的输出。
/// </summary>
public sealed class DevServiceLog : IDisposable
{
    /// <summary>内存里保留的最大行数，避免长时间运行的服务把内存吃满。</summary>
    public const int MaxLines = 2000;

    /// <summary>严格 UTF-8 解码：遇到非法字节就抛异常，以便回退到系统 ANSI 代码页。</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>系统 ANSI 代码页（简体中文 Windows 为 GBK）。</summary>
    private static readonly Encoding AnsiEncoding = CreateAnsiEncoding();

    private readonly object _gate = new();
    private readonly Queue<string> _lines = new();
    private readonly StreamWriter? _writer;
    private bool _disposed;

    public DevServiceLog(string? filePath = null)
    {
        FilePath = filePath;
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            _writer = new StreamWriter(filePath, append: false, new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch
        {
            _writer = null;
        }
    }

    public string? FilePath { get; }

    public void Append(string line, bool isError = false)
    {
        var text = isError ? $"[stderr] {line}" : line;
        lock (_gate)
        {
            _lines.Enqueue(text);
            while (_lines.Count > MaxLines)
                _lines.Dequeue();

            try
            {
                _writer?.WriteLine(text);
            }
            catch
            {
                // 磁盘不可写时只保留内存日志。
            }
        }
    }

    /// <summary>写一条带时间戳的系统消息（启动、停止、退出等）。</summary>
    public void AppendSystem(string message) => Append($"[{DateTime.Now:HH:mm:ss}] {message}");

    public string GetText()
    {
        lock (_gate)
            return string.Join(Environment.NewLine, _lines);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _lines.Clear();
            try
            {
                if (_writer is not null)
                {
                    _writer.BaseStream.SetLength(0);
                    _writer.BaseStream.Position = 0;
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            try
            {
                _writer?.Dispose();
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// 按行解码子进程输出：先按严格 UTF-8 解，非法字节再按系统 ANSI 代码页解。
    /// PostgreSQL 等原生程序在中文 Windows 上按 GBK 输出，而 npm / node 多为 UTF-8，
    /// 单靠一种编码必然有一边乱码。
    /// </summary>
    public static string DecodeLine(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            text = AnsiEncoding.GetString(bytes);
        }

        // 去掉可能出现在首行的 UTF-8 BOM。
        return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
    }

    /// <summary>日志文件按「名称 + Id」命名，便于在资源管理器里直接辨认。</summary>
    public static string GetLogPath(string name, string id)
    {
        var safeName = Sanitize(name);
        var safeId = Sanitize(id);
        var fileName = safeName.Length == 0
            ? safeId.Length == 0 ? "service.log" : $"service-{safeId}.log"
            : safeId.Length == 0 ? $"{safeName}.log" : $"{safeName}-{safeId}.log";
        return Path.Combine(AppPaths.LogsDirectory, fileName);
    }

    /// <summary>读取日志文件末尾若干行（程序重启后没有内存日志时用）。</summary>
    public static string ReadTail(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return string.Empty;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var lines = new Queue<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Enqueue(line);
                while (lines.Count > MaxLines)
                    lines.Dequeue();
            }

            return string.Join(Environment.NewLine, lines);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static void Delete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// 取系统 ANSI 代码页（简体中文 Windows 为 GBK）。
    /// .NET 只内置 UTF-8 / UTF-16 等少数编码，GBK 需要显式注册代码页提供程序。
    /// </summary>
    private static Encoding CreateAnsiEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(0);
        }
        catch
        {
            return Encoding.Latin1;
        }
    }

    private static string Sanitize(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        text = new string(text.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim('-', ' ');
        return text.Length > 32 ? text[..32] : text;
    }
}
