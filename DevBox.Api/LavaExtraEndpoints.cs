using System.Diagnostics;
using DevEnv;
using DevEnv.Models;
using DevEnv.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DevBox.Api;

public static class LavaExtraEndpoints
{
    public static void MapLavaExtraEndpoints(this WebApplication app)
    {
        DevEnvBootstrap.EnsureInitialized();

        app.MapGet("/api/lava/downloads", () =>
        {
            var all = AppServices.DownloadHistory.GetAll();
            var stats = AppServices.DownloadHistory.GetStatistics();
            return Results.Json(new
            {
                items = all.Select(MapDownload),
                stats = new
                {
                    total = stats.Total,
                    completed = stats.Completed,
                    failed = stats.Failed,
                    downloading = stats.Downloading,
                    totalBytes = stats.TotalBytes,
                    totalBytesText = FormatBytes(stats.TotalBytes),
                },
            });
        });

        app.MapPost("/api/lava/downloads/start", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var url = doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
            var fileName = doc.RootElement.TryGetProperty("fileName", out var f) ? f.GetString() : null;
            var software = doc.RootElement.TryGetProperty("softwareName", out var s) ? s.GetString() ?? "software" : "software";
            var version = doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(url))
                return Results.BadRequest(new { error = "需要 url" });
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = InferFileName(url, software, version);

            var id = await AppServices.Download.StartDownloadAsync(url, fileName, software, version);
            return Results.Ok(new { ok = true, id });
        });

        app.MapPost("/api/lava/downloads/{id}/cancel", (string id) =>
        {
            AppServices.Download.CancelDownload(id);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/lava/downloads/clear-completed", () =>
        {
            AppServices.DownloadHistory.ClearCompleted();
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/lava/downloads/clear-failed", () =>
        {
            AppServices.DownloadHistory.ClearFailed();
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/lava/downloads/clear-all", () =>
        {
            AppServices.DownloadHistory.ClearAll();
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/lava/downloads/open-cache-dir", () =>
        {
            var dir = AppServices.Config.Load().CacheDir;
            if (string.IsNullOrWhiteSpace(dir))
                dir = DevEnv.Models.AppPaths.CacheDir;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = dir, UseShellExecute = true });
            return Results.Ok(new { ok = true, path = dir });
        });

        app.MapPost("/api/lava/downloads/{id}/open-file", (string id) =>
        {
            var record = AppServices.DownloadHistory.GetById(id);
            if (record is null)
                return Results.NotFound(new { error = "记录不存在" });
            if (string.IsNullOrWhiteSpace(record.SavePath) || !File.Exists(record.SavePath))
                return Results.BadRequest(new { error = "文件不存在" });
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{record.SavePath}\"",
                UseShellExecute = true,
            });
            return Results.Ok(new { ok = true, path = record.SavePath });
        });

        app.MapGet("/api/lava/software-catalog", () =>
        {
            var config = ResourceLoader.LoadBundledJson<Dictionary<string, List<SoftwareCatalogItem>>>(
                ResourcePaths.SoftwareCatalogFile);
            if (config is null)
                return Results.Json(new { categories = Array.Empty<object>() });

            var categories = config.Select(kv => new
            {
                category = kv.Key,
                items = kv.Value.Select(item => new
                {
                    name = item.Name,
                    description = item.Desc,
                    icon = item.Icon,
                    versions = item.Versions.Select(v => new
                    {
                        version = v.Version,
                        url = v.Url,
                        fileName = string.IsNullOrWhiteSpace(v.FileName)
                            ? InferFileName(v.Url, item.Name, v.Version)
                            : v.FileName,
                    }),
                }),
            });
            return Results.Json(new { categories });
        });

        app.MapGet("/api/lava/mirrors", async (string? tool) =>
        {
            if (!Enum.TryParse<MirrorToolType>(tool, true, out var toolType))
                toolType = MirrorToolType.Pip;

            var mirrors = AppServices.MirrorConfig.GetMirrors(toolType);
            var current = await AppServices.MirrorConfig.GetCurrentMirrorAsync(toolType);
            return Results.Json(new
            {
                tool = toolType.ToString(),
                tools = Enum.GetValues<MirrorToolType>().Select(t => new
                {
                    key = t.ToString(),
                    label = ToolLabel(t),
                }),
                current,
                mirrors = mirrors.Select(m => new
                {
                    key = m.Key,
                    name = m.Name,
                    url = m.Url,
                    responseTimeMs = m.ResponseTimeMs,
                }),
            });
        });

        app.MapPost("/api/lava/mirrors/test", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var tool = doc.RootElement.TryGetProperty("tool", out var t) ? t.GetString() : "Pip";
            if (!Enum.TryParse<MirrorToolType>(tool, true, out var toolType))
                return Results.BadRequest(new { error = "未知工具" });

            var mirrors = AppServices.MirrorConfig.GetMirrors(toolType);
            var results = await AppServices.MirrorConfig.TestMirrorSpeedAsync(mirrors);
            var fastest = results.FirstOrDefault(m => m.ResponseTimeMs.HasValue);
            return Results.Json(new
            {
                tool = toolType.ToString(),
                mirrors = results.Select(m => new
                {
                    key = m.Key,
                    name = m.Name,
                    url = m.Url,
                    responseTimeMs = m.ResponseTimeMs,
                }),
                recommended = fastest is null
                    ? null
                    : new { key = fastest.Key, name = fastest.Name, responseTimeMs = fastest.ResponseTimeMs },
            });
        });

        app.MapPost("/api/lava/mirrors/configure", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var tool = doc.RootElement.TryGetProperty("tool", out var t) ? t.GetString() : "Pip";
            var key = doc.RootElement.TryGetProperty("key", out var k) ? k.GetString() : null;
            if (!Enum.TryParse<MirrorToolType>(tool, true, out var toolType))
                return Results.BadRequest(new { error = "未知工具" });
            var mirror = AppServices.MirrorConfig.GetMirrors(toolType)
                .FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
            if (mirror is null)
                return Results.BadRequest(new { error = "未知镜像" });

            var (ok, message) = await AppServices.MirrorConfig.ConfigureMirrorAsync(toolType, mirror);
            return ok ? Results.Ok(new { ok, message }) : Results.BadRequest(new { error = message });
        });
    }

    private static object MapDownload(DownloadRecord r) => new
    {
        id = r.Id,
        softwareName = r.SoftwareName,
        version = r.Version,
        fileName = r.FileName,
        filePath = r.SavePath,
        url = r.Url,
        status = r.Status.ToString(),
        statusText = r.StatusText,
        progress = r.Progress,
        speed = r.Speed,
        speedText = r.SpeedText,
        sizeText = r.SizeText,
        startTime = r.StartTime,
        endTime = r.EndTime,
        errorMessage = r.ErrorMessage,
        canOpen = !string.IsNullOrWhiteSpace(r.SavePath) && File.Exists(r.SavePath),
        isActive = r.Status is DownloadStatus.Pending or DownloadStatus.Downloading,
    };

    private static string ToolLabel(MirrorToolType tool) => tool switch
    {
        MirrorToolType.Pip => "pip (Python)",
        MirrorToolType.Npm => "npm (Node.js)",
        MirrorToolType.Pnpm => "pnpm (Node.js)",
        MirrorToolType.Yarn => "Yarn (Node.js)",
        MirrorToolType.Maven => "Maven (Java)",
        MirrorToolType.Composer => "Composer (PHP)",
        MirrorToolType.Go => "Go 模块代理",
        MirrorToolType.NuGet => "NuGet (.NET)",
        _ => tool.ToString(),
    };

    private static string InferFileName(string url, string softwareName, string version)
    {
        if (string.IsNullOrEmpty(url))
            return $"{softwareName}_{version}.exe";
        try
        {
            var uri = new Uri(url);
            var name = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrEmpty(name) && name != "/")
                return name;
        }
        catch
        {
            // ignore
        }

        var ext = Path.GetExtension(url);
        return $"{softwareName}_{version}{ext}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
