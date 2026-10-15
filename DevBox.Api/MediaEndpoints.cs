using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DevBox.Api;

public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this WebApplication app)
    {
        // 图标 / 剪贴板图片：用 query path，仅允许用户数据目录下的文件
        app.MapGet("/api/media/file", (string path) =>
        {
            if (string.IsNullOrWhiteSpace(path))
                return Results.BadRequest(new { error = "缺少 path" });

            string full;
            try { full = Path.GetFullPath(path); }
            catch { return Results.BadRequest(new { error = "路径无效" }); }

            if (!File.Exists(full))
                return Results.NotFound();

            var allowedRoots = new[]
            {
                Path.GetFullPath(IconHelper.IconsDirectory),
                Path.GetFullPath(ClipboardApp.Services.AppPaths.ImagesDir),
                Path.GetFullPath(AppPaths.UserDataDirectory),
            };
            if (!allowedRoots.Any(root => full.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
                return Results.Json(new { error = "路径不在允许范围" }, statusCode: 403);

            var contentType = Path.GetExtension(full).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".ico" => "image/x-icon",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream",
            };
            return Results.File(full, contentType);
        });
    }
}
