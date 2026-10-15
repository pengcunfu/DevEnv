using System.Text;
using System.Text.Json;
using DevEnv.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DevBox.Api;

public static class UtilityEndpoints
{
    private static readonly string HostsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "drivers", "etc", "hosts");

    public static void MapUtilityEndpoints(this WebApplication app)
    {
        app.MapPost("/api/utils/json/format", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var text = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            var minify = doc.RootElement.TryGetProperty("minify", out var m) && m.GetBoolean();
            try
            {
                using var parsed = JsonDocument.Parse(text);
                var result = minify
                    ? JsonSerializer.Serialize(parsed.RootElement)
                    : JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions { WriteIndented = true });
                return Results.Json(new { ok = true, text = result });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/utils/hash", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var algo = doc.RootElement.TryGetProperty("algorithm", out var a) ? a.GetString() ?? "SHA256" : "SHA256";
            var path = doc.RootElement.TryGetProperty("path", out var p) ? p.GetString() : null;
            var text = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() : null;
            if (!Enum.TryParse<HashType>(algo, true, out var hashType))
                hashType = HashType.SHA256;

            var svc = new HashCalculatorService();
            try
            {
                HashResult result;
                if (!string.IsNullOrWhiteSpace(path))
                    result = await svc.CalculateFileHashAsync(path, hashType);
                else if (!string.IsNullOrEmpty(text))
                    result = await svc.CalculateTextHashAsync(text, hashType);
                else
                    return Results.BadRequest(new { error = "请提供 path 或 text" });

                return Results.Json(new
                {
                    ok = true,
                    hash = result.Hash,
                    algorithm = result.Algorithm,
                    fileSize = result.FileSize,
                    elapsedMs = result.CalculationTime.TotalMilliseconds,
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/utils/hosts", () =>
        {
            try
            {
                var text = File.Exists(HostsPath) ? File.ReadAllText(HostsPath, Encoding.UTF8) : "";
                return Results.Json(new { path = HostsPath, text, canWrite = ElevationHelper.IsAdministrator() });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPut("/api/utils/hosts", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var text = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            try
            {
                File.WriteAllText(HostsPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return Results.Ok(new { ok = true });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { error = "需要管理员权限", needElevation = true }, statusCode: 403);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/utils/hosts/elevate", () =>
        {
            var ok = ElevationHelper.TryRunElevated(ElevationHelper.EditHostsArgument);
            return ok
                ? Results.Ok(new { ok = true, message = "已请求管理员权限打开 Hosts 编辑" })
                : Results.BadRequest(new { error = "用户取消了 UAC 或无法提权" });
        });

        app.MapPost("/api/utils/image/convert", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var format = doc.RootElement.TryGetProperty("format", out var f) ? f.GetString() ?? "png" : "png";
            var outputDir = doc.RootElement.TryGetProperty("outputDirectory", out var o) ? o.GetString() : null;
            var quality = doc.RootElement.TryGetProperty("quality", out var q) ? q.GetInt32() : 95;
            var files = new List<string>();
            if (doc.RootElement.TryGetProperty("files", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var path = item.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                        files.Add(path);
                }
            }

            if (files.Count == 0)
                return Results.BadRequest(new { error = "请提供 files" });

            var svc = new ImageConversionService();
            var results = await svc.ConvertImagesAsync(files, format, outputDir, quality, overwriteExisting: true);
            return Results.Json(new
            {
                results = results.Select(r => new
                {
                    input = r.InputFile,
                    output = r.OutputFile,
                    success = r.Success,
                    error = r.ErrorMessage,
                }),
            });
        });
    }
}
