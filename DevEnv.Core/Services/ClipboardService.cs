using System.IO;
using DevEnv.Core.Clipboard.Models;
using DevEnv.Core.Clipboard.Services;
using DevEnv.Core.Models;

namespace DevEnv.Core.Services;

/// <summary>
/// 熔岩剪贴板历史。逻辑从 <c>ClipboardEndpoints</c> 原样平移；
/// 原先的 <c>/api/media/file</c> 图片接口变成 <see cref="ClipboardEntryView.ImageFullPath"/>，
/// WPF 侧直接绑定本地文件，不再需要起 HTTP 服务传图片。
/// </summary>
public sealed class ClipboardService
{
    public ClipboardService()
    {
        DevEnv.Core.Clipboard.Services.AppPaths.MigrateLegacyData();
    }

    /// <summary>底层历史服务：剪贴板轮询捕获到新内容时直接写入。</summary>
    public HistoryService History { get; } = new();

    public IReadOnlyList<ClipboardEntryView> GetEntries(string? search = null, string? type = null)
    {
        History.ReloadView(new HistoryQuery(
            SinceTimestamp: HistoryService.RetentionCutoff(),
            Search: search,
            Type: type));
        return History.Entries.Select(MapEntry).ToList();
    }

    /// <summary>保留窗口内的统计，供状态栏展示。</summary>
    public ClipboardStatsView GetStats() => History.GetStats();

    public OperationResult Delete(long id)
    {
        var entry = FindEntry(id);
        if (entry is null)
            return OperationResult.Fail("记录不存在");
        History.Delete(entry);
        return OperationResult.Success();
    }

    public int Clear(string mode = "all", string? type = null) => History.ClearByRange(mode, type);

    public OperationResult Paste(long id)
    {
        var entry = FindEntry(id);
        if (entry is null)
            return OperationResult.Fail("记录不存在");

        if (entry.IsImage)
        {
            var path = ResolveImagePath(entry.ImagePath);
            if (path is null)
                return OperationResult.Fail("图片文件不存在");
            HostCallbacks.SetClipboardImagePath?.Invoke(path);
        }
        else
        {
            if (string.IsNullOrEmpty(entry.Text))
                return OperationResult.Fail("空文本");
            HostCallbacks.SetClipboardText?.Invoke(entry.Text);
        }

        return OperationResult.Success("已写入系统剪贴板");
    }

    public OperationResult Export()
    {
        History.ReloadView(History.GetCurrentQuery());
        var suggested = $"clipboard_history_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var path = HostCallbacks.SaveFile?.Invoke(suggested, "文本文件|*.txt|所有文件|*.*");
        if (string.IsNullOrWhiteSpace(path))
            return OperationResult.Success("已取消导出");

        try
        {
            using var writer = new StreamWriter(path);
            foreach (var entry in History.Entries)
            {
                if (!entry.IsImage && !string.IsNullOrEmpty(entry.Text))
                    writer.WriteLine(entry.Text);
                writer.WriteLine();
            }
            return OperationResult.Success(path);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult CaptureText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return OperationResult.Fail("空文本");

        History.Insert(new ClipboardEntry
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Type = "text",
            Text = text,
        });
        return OperationResult.Success();
    }

    /// <summary>图片条目落盘（剪贴板轮询捕获到图片时由宿主调用）。</summary>
    public OperationResult CaptureImage(byte[] pngBytes)
    {
        try
        {
            var dir = DevEnv.Core.Clipboard.Services.AppPaths.ImagesDir;
            Directory.CreateDirectory(dir);
            var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            File.WriteAllBytes(Path.Combine(dir, fileName), pngBytes);
            History.Insert(new ClipboardEntry
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Type = "image",
                ImagePath = fileName,
            });
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private ClipboardEntry? FindEntry(long id)
    {
        var entry = History.Entries.FirstOrDefault(e => e.Id == id);
        if (entry is not null)
            return entry;
        History.ReloadView(new HistoryQuery());
        return History.Entries.FirstOrDefault(e => e.Id == id);
    }

    public static string? ResolveImagePath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;
        if (File.Exists(imagePath))
            return Path.GetFullPath(imagePath);
        var combined = Path.Combine(DevEnv.Core.Clipboard.Services.AppPaths.ImagesDir, imagePath);
        return File.Exists(combined) ? combined : null;
    }

    private static ClipboardEntryView MapEntry(ClipboardEntry e) => new()
    {
        Id = e.Id,
        Timestamp = e.Timestamp,
        Type = e.Type,
        Text = e.Text,
        ImagePath = e.ImagePath,
        ImageFullPath = e.IsImage ? ResolveImagePath(e.ImagePath) : null,
        DisplayText = e.DisplayText,
        IsImage = e.IsImage,
    };
}