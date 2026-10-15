using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DevBox.Api;

public static class DevEnvEndpoints
{
    public static void MapDevEnvEndpoints(this WebApplication app)
    {
        app.MapGet("/api/devenv/services", () =>
        {
            var list = DevServicesManager.Services.Select(s =>
            {
                var running = DevServicesManager.IsRunning(s);
                var installed = DevServicesManager.IsInstalled(s);
                return new
                {
                    key = s.Key,
                    name = s.Name,
                    description = s.Description,
                    installPath = s.InstallPath,
                    dataPath = s.DataPath,
                    running,
                    installed,
                    statusText = !installed ? "未安装" : running ? "运行中" : "已停止",
                    canStart = installed && !running,
                    canStop = installed && running,
                };
            });
            return Results.Json(list);
        });

        app.MapPost("/api/devenv/services/{key}/start", (string key) =>
        {
            var svc = DevServicesManager.Find(key);
            if (svc is null)
                return Results.NotFound(new { error = "未知服务" });
            try
            {
                DevServicesManager.Start(svc);
                return Results.Ok(new { ok = true, message = $"{svc.Name} 已启动" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/devenv/services/{key}/stop", (string key) =>
        {
            var svc = DevServicesManager.Find(key);
            if (svc is null)
                return Results.NotFound(new { error = "未知服务" });
            try
            {
                DevServicesManager.Stop(svc);
                return Results.Ok(new { ok = true, message = $"{svc.Name} 已停止" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/devenv/services/{key}/open-data", (string key) =>
        {
            var svc = DevServicesManager.Find(key);
            if (svc is null)
                return Results.NotFound(new { error = "未知服务" });
            var dir = Directory.Exists(svc.DataPath) ? svc.DataPath : svc.InstallPath;
            if (!Directory.Exists(dir))
                return Results.BadRequest(new { error = "目录不存在" });
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{dir}\"",
                UseShellExecute = true,
            });
            return Results.Ok(new { ok = true, path = dir });
        });
    }
}
