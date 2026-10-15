using ClipboardApp.Models;
using ClipboardApp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DevBox.Api;

public static class ClipboardEndpoints
{
    private static readonly HistoryService History = CreateHistory();

    private static HistoryService CreateHistory()
    {
        ClipboardApp.Services.AppPaths.MigrateLegacyData();
        return new HistoryService();
    }

    public static HistoryService SharedHistory => History;

    public static void MapClipboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/clipboard", (string? search, string? type) =>
        {
            History.ReloadView(new HistoryQuery(Search: search, Type: type));
            var items = History.Entries.Select(MapEntry);
            return Results.Json(items);
        });

        app.MapGet("/api/clipboard/{id:long}", (long id) =>
        {
            var entry = FindEntry(id);
            return entry is null ? Results.NotFound() : Results.Json(MapEntry(entry));
        });

        app.MapDelete("/api/clipboard/{id:long}", (long id) =>
        {
            var entry = FindEntry(id);
            if (entry is null)
                return Results.NotFound();
            History.Delete(entry);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/clipboard/clear", (string? mode) =>
        {
            var removed = History.ClearByRange(mode ?? "all");
            return Results.Ok(new { ok = true, removed });
        });

        app.MapPost("/api/clipboard/{id:long}/paste", (long id) =>
        {
            var entry = FindEntry(id);
            if (entry is null)
                return Results.NotFound();

            if (entry.IsImage)
            {
                var path = ResolveImagePath(entry.ImagePath);
                if (path is null)
                    return Results.BadRequest(new { error = "图片文件不存在" });
                HostCallbacks.SetClipboardImagePath?.Invoke(path);
            }
            else
            {
                if (string.IsNullOrEmpty(entry.Text))
                    return Results.BadRequest(new { error = "空文本" });
                HostCallbacks.SetClipboardText?.Invoke(entry.Text);
            }

            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/clipboard/export", () =>
        {
            History.ReloadView(new HistoryQuery());
            var suggested = $"clipboard_history_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var path = HostCallbacks.SaveFile?.Invoke(suggested, "文本文件|*.txt|所有文件|*.*");
            if (path is null)
                return Results.Json(new { cancelled = true });

            try
            {
                using var writer = new StreamWriter(path);
                foreach (var entry in History.Entries)
                {
                    if (!entry.IsImage && !string.IsNullOrEmpty(entry.Text))
                        writer.WriteLine(entry.Text);
                    writer.WriteLine();
                }
                return Results.Ok(new { ok = true, path });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/clipboard/capture-text", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var text = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { error = "空文本" });

            var entry = new ClipboardEntry
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Type = "text",
                Text = text,
            };
            History.Insert(entry);
            return Results.Ok(new { ok = true });
        });
    }

    private static ClipboardEntry? FindEntry(long id)
    {
        var entry = History.Entries.FirstOrDefault(e => e.Id == id);
        if (entry is not null)
            return entry;
        History.ReloadView(new HistoryQuery());
        return History.Entries.FirstOrDefault(e => e.Id == id);
    }

    private static string? ResolveImagePath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;
        if (File.Exists(imagePath))
            return Path.GetFullPath(imagePath);
        var combined = Path.Combine(ClipboardApp.Services.AppPaths.ImagesDir, imagePath);
        return File.Exists(combined) ? combined : null;
    }

    private static object MapEntry(ClipboardEntry e)
    {
        var imageFull = e.IsImage ? ResolveImagePath(e.ImagePath) : null;
        return new
        {
            id = e.Id,
            timestamp = e.Timestamp,
            type = e.Type,
            text = e.Text,
            imagePath = e.ImagePath,
            imageUrl = imageFull is null ? null : $"/api/media/file?path={Uri.EscapeDataString(imageFull)}",
            displayText = e.DisplayText,
            isImage = e.IsImage,
        };
    }
}
