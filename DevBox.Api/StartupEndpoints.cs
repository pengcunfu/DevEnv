using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Win32;

namespace DevBox.Api;

public static class StartupEndpoints
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void MapStartupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/startup", () =>
        {
            var list = new List<object>();

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                if (key is not null)
                {
                    foreach (var name in key.GetValueNames())
                    {
                        var cmd = key.GetValue(name)?.ToString() ?? "";
                        list.Add(new
                        {
                            id = name,
                            name,
                            command = cmd,
                            location = "Registry",
                            enabled = true,
                        });
                    }
                }
            }
            catch { /* ignore */ }

            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(startupFolder))
            {
                foreach (var file in Directory.EnumerateFiles(startupFolder))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    list.Add(new
                    {
                        id = file,
                        name,
                        command = file,
                        location = "StartupFolder",
                        enabled = true,
                    });
                }
            }

            return Results.Json(list);
        });

        app.MapPost("/api/startup", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
            var command = doc.RootElement.TryGetProperty("command", out var c) ? c.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(command))
                return Results.BadRequest(new { error = "需要 name 与 command" });

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                key?.SetValue(name, command);
                return Results.Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapDelete("/api/startup/{id}", (string id) =>
        {
            try
            {
                if (File.Exists(id))
                {
                    File.Delete(id);
                    return Results.Ok(new { ok = true });
                }

                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                key?.DeleteValue(Uri.UnescapeDataString(id), throwOnMissingValue: false);
                return Results.Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}
