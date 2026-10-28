using System.Text;
using DevEnv.Core.Services;

namespace DevEnv.Tests;

public sealed class DevServiceLogTests
{
    [Fact]
    public void Append_KeepsOnlyNewestLines()
    {
        using var log = new DevServiceLog();
        for (var i = 0; i < DevServiceLog.MaxLines + 5; i++)
            log.Append($"line {i}");

        var lines = log.GetText().Split(Environment.NewLine);

        Assert.Equal(DevServiceLog.MaxLines, lines.Length);
        Assert.Equal("line 5", lines[0]);
        Assert.Equal($"line {DevServiceLog.MaxLines + 4}", lines[^1]);
    }

    [Fact]
    public void FileLog_WritesToDisk_AndReadTailReturnsContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"devenv-log-{Guid.NewGuid():N}.log");
        try
        {
            using (var log = new DevServiceLog(path))
            {
                log.Append("hello");
                log.Append("oops", isError: true);
            }

            var text = DevServiceLog.ReadTail(path);
            Assert.Contains("hello", text);
            Assert.Contains("[stderr] oops", text);
        }
        finally
        {
            DevServiceLog.Delete(path);
        }
    }

    [Fact]
    public void Clear_EmptiesMemoryAndFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"devenv-log-{Guid.NewGuid():N}.log");
        try
        {
            using var log = new DevServiceLog(path);
            log.Append("hello");
            log.Clear();

            Assert.Equal(string.Empty, log.GetText());
            Assert.Equal(string.Empty, DevServiceLog.ReadTail(path));
        }
        finally
        {
            DevServiceLog.Delete(path);
        }
    }

    [Fact]
    public void DecodeLine_PrefersUtf8()
    {
        var bytes = Encoding.UTF8.GetBytes("中文日志");

        Assert.Equal("中文日志", DevServiceLog.DecodeLine(bytes));
    }

    [Fact]
    public void DecodeLine_FallsBackToAnsiCodePageForGbkViaPostgreSql()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(936).GetBytes("数据库启动成功");

        Assert.Equal("数据库启动成功", DevServiceLog.DecodeLine(bytes));
    }

    [Fact]
    public void DecodeLine_StripsUtf8Bom()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("hello"))
            .ToArray();

        Assert.Equal("hello", DevServiceLog.DecodeLine(bytes));
    }

    [Fact]
    public void GetLogPath_ReplacesInvalidFileNameChars()
    {
        var path = DevServiceLog.GetLogPath("my:service", "id/1");
        var fileName = Path.GetFileName(path);

        Assert.DoesNotContain(':', fileName);
        Assert.DoesNotContain('/', fileName);
        Assert.EndsWith(".log", fileName, StringComparison.OrdinalIgnoreCase);
    }
}
