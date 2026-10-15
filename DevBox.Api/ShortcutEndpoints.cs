using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;

namespace DevBox.Api;

public static class ShortcutEndpoints
{
    private static IResult JsonArray(JArray list) =>
        Results.Content(list.ToString(Newtonsoft.Json.Formatting.None), "application/json", Encoding.UTF8);

    public static void MapShortcutEndpoints(this WebApplication app)
    {
        app.MapGet("/api/native-apps", (DataStore store) =>
            JsonArray(store.GetList("NativeAtomList")));

        app.MapPost("/api/native-apps", async (HttpRequest req, DataStore store) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var path = doc.RootElement.TryGetProperty("path", out var p) ? p.GetString() : null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return Results.BadRequest(new { error = "无效的 exe 路径" });

            var name = IconHelper.GetDisplayName(path);
            var icon = IconHelper.ExtractExecutableIcon(path) ?? "";
            var list = store.GetList("NativeAtomList");
            var existing = list.FirstOrDefault(x =>
                string.Equals(x?["ExecPath"]?.ToString(), path, StringComparison.OrdinalIgnoreCase));
            if (existing is JObject obj)
            {
                obj["Name"] = name;
                obj["Description"] = path;
                if (string.IsNullOrWhiteSpace(obj["IconPath"]?.ToString()))
                    obj["IconPath"] = icon;
            }
            else
            {
                list.Add(new JObject
                {
                    ["Name"] = name,
                    ["IconPath"] = icon,
                    ["Tag"] = "native",
                    ["Description"] = path,
                    ["ExecPath"] = path,
                });
            }

            store.SaveList("NativeAtomList", list);
            return Results.Ok(new { ok = true, name });
        });

        app.MapPut("/api/native-apps/{index:int}", async (int index, HttpRequest req, DataStore store) =>
        {
            var list = store.GetList("NativeAtomList");
            if (index < 0 || index >= list.Count || list[index] is not JObject item)
                return Results.NotFound();

            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (doc.RootElement.TryGetProperty("name", out var n) && n.GetString() is { } name)
                item["Name"] = name;
            if (doc.RootElement.TryGetProperty("execPath", out var e) && e.GetString() is { } exec && File.Exists(exec))
            {
                item["ExecPath"] = exec;
                item["Description"] = exec;
            }
            if (doc.RootElement.TryGetProperty("iconPath", out var ic) && ic.GetString() is { } iconPath)
                item["IconPath"] = iconPath;
            if (doc.RootElement.TryGetProperty("refreshIcon", out var ri) && ri.GetBoolean())
            {
                var path = item["ExecPath"]?.ToString();
                if (!string.IsNullOrWhiteSpace(path))
                    item["IconPath"] = IconHelper.ExtractExecutableIcon(path) ?? item["IconPath"];
            }

            store.SaveList("NativeAtomList", list);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/native-apps/{index:int}/run", (int index, DataStore store) =>
        {
            var list = store.GetList("NativeAtomList");
            if (index < 0 || index >= list.Count)
                return Results.NotFound();
            var path = list[index]?["ExecPath"]?.ToString();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return Results.BadRequest(new { error = "可执行文件不存在" });
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory,
            });
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/native-apps/{index:int}/open-folder", (int index, DataStore store) =>
        {
            var list = store.GetList("NativeAtomList");
            if (index < 0 || index >= list.Count)
                return Results.NotFound();
            var path = list[index]?["ExecPath"]?.ToString();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return Results.BadRequest(new { error = "可执行文件不存在" });
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
            return Results.Ok(new { ok = true });
        });

        app.MapDelete("/api/native-apps/{index:int}", (int index, DataStore store) =>
        {
            var list = store.GetList("NativeAtomList");
            if (index < 0 || index >= list.Count)
                return Results.NotFound();
            list.RemoveAt(index);
            store.SaveList("NativeAtomList", list);
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/folders", (DataStore store) =>
            JsonArray(store.GetList("FolderAtomList")));

        app.MapPost("/api/folders", async (HttpRequest req, DataStore store) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var path = doc.RootElement.TryGetProperty("path", out var p) ? p.GetString() : null;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return Results.BadRequest(new { error = "无效的文件夹路径" });

            var name = new DirectoryInfo(path).Name;
            var list = store.GetList("FolderAtomList");
            list.Add(new JObject
            {
                ["Name"] = name,
                ["IconPath"] = "",
                ["Tag"] = "folder",
                ["Description"] = path,
                ["FolderPath"] = path,
            });
            store.SaveList("FolderAtomList", list);
            return Results.Ok(new { ok = true, name });
        });

        app.MapPut("/api/folders/{index:int}", async (int index, HttpRequest req, DataStore store) =>
        {
            var list = store.GetList("FolderAtomList");
            if (index < 0 || index >= list.Count || list[index] is not JObject item)
                return Results.NotFound();
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (doc.RootElement.TryGetProperty("name", out var n) && n.GetString() is { } name)
                item["Name"] = name;
            if (doc.RootElement.TryGetProperty("folderPath", out var fp) && fp.GetString() is { } folder && Directory.Exists(folder))
            {
                item["FolderPath"] = folder;
                item["Description"] = folder;
            }
            store.SaveList("FolderAtomList", list);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/folders/{index:int}/open", (int index, DataStore store) =>
        {
            var list = store.GetList("FolderAtomList");
            if (index < 0 || index >= list.Count)
                return Results.NotFound();
            var path = list[index]?["FolderPath"]?.ToString();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return Results.BadRequest(new { error = "文件夹不存在" });
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true,
            });
            return Results.Ok(new { ok = true });
        });

        app.MapDelete("/api/folders/{index:int}", (int index, DataStore store) =>
        {
            var list = store.GetList("FolderAtomList");
            if (index < 0 || index >= list.Count)
                return Results.NotFound();
            list.RemoveAt(index);
            store.SaveList("FolderAtomList", list);
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/webs", (DataStore store) =>
            JsonArray(store.GetList("WebAtomList")));

        app.MapPost("/api/webs", async (HttpRequest req, DataStore store) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var urlInput = doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
            var titleOverride = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(urlInput))
                return Results.BadRequest(new { error = "缺少 url" });

            string url, title, iconPath;
            try
            {
                (url, title, iconPath) = await WebMetadataHelper.FetchAsync(urlInput);
            }
            catch (Exception ex)
            {
                var normalized = WebMetadataHelper.NormalizeUrl(urlInput);
                if (normalized is null)
                    return Results.BadRequest(new { error = ex.Message });
                url = normalized;
                title = new Uri(url).Host;
                iconPath = "";
            }

            if (!string.IsNullOrWhiteSpace(titleOverride))
                title = titleOverride;

            var list = store.GetList("WebAtomList");
            list.Add(new JObject
            {
                ["Name"] = title,
                ["IconPath"] = iconPath,
                ["Tag"] = "web",
                ["Description"] = url,
                ["Url"] = url,
            });
            store.SaveList("WebAtomList", list);
            return Results.Ok(new { ok = true, name = title, url });
        });

        app.MapPut("/api/webs/{index:int}", async (int index, HttpRequest req, DataStore store) =>
        {
            var list = store.GetList("WebAtomList");
            if (index < 0 || index >= list.Count || list[index] is not JObject item)
                return Results.NotFound();
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (doc.RootElement.TryGetProperty("name", out var n) && n.GetString() is { } name)
                item["Name"] = name;
            if (doc.RootElement.TryGetProperty("url", out var u) && u.GetString() is { } urlRaw)
            {
                var url = WebMetadataHelper.NormalizeUrl(urlRaw);
                if (url is not null)
                {
                    item["Url"] = url;
                    item["Description"] = url;
                }
            }
            if (doc.RootElement.TryGetProperty("refreshIcon", out var ri) && ri.GetBoolean())
            {
                try
                {
                    var (_, _, icon) = await WebMetadataHelper.FetchAsync(item["Url"]?.ToString() ?? "");
                    item["IconPath"] = icon;
                }
                catch { /* ignore */ }
            }
            store.SaveList("WebAtomList", list);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/webs/{index:int}/open", (int index, DataStore store) =>
        {
            var list = store.GetList("WebAtomList");
            if (index < 0 || index >= list.Count)
                return Results.NotFound();
            var url = list[index]?["Url"]?.ToString();
            if (string.IsNullOrWhiteSpace(url))
                return Results.BadRequest(new { error = "网址无效" });
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return Results.Ok(new { ok = true });
        });

        app.MapDelete("/api/webs/{index:int}", (int index, DataStore store) =>
        {
            var list = store.GetList("WebAtomList");
            if (index < 0 || index >= list.Count)
                return Results.NotFound();
            list.RemoveAt(index);
            store.SaveList("WebAtomList", list);
            return Results.Ok(new { ok = true });
        });

        // 实用工具 / 启动项管理已迁到顶部菜单，系统工具页只保留 Windows 系统入口
        var menuOnlyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "json_formatter", "hosts_editor", "image_converter", "hash_calculator", "startup",
        };
        app.MapGet("/api/system-tools", () =>
            Results.Json(WindowsToolRunner.Catalog
                .Where(t => !menuOnlyKeys.Contains(t.Key))
                .Select(t => new
                {
                    key = t.Key,
                    name = t.Name,
                    category = t.Category,
                    description = t.Description,
                    icon = $"/icons/win/{t.Key}.png",
                })));

        app.MapPost("/api/system-tools/{key}/run", (string key) =>
        {
            if (!WindowsToolRunner.TryRun(key, out var error))
                return Results.BadRequest(new { error });
            return Results.Ok(new { ok = true });
        });
    }
}
