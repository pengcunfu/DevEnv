using System.Diagnostics;
using DevEnv;
using DevEnv.Models;
using DevEnv.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DevBox.Api;

public static class LavaEndpoints
{
    public static void MapLavaEndpoints(this WebApplication app)
    {
        DevEnvBootstrap.EnsureInitialized();
        AppServices.ProcessManager.StartMonitoring();

        app.MapGet("/api/lava/processes", () =>
        {
            var pm = AppServices.ProcessManager;
            var list = pm.GetDefinitions().Select(def =>
            {
                var status = pm.GetProcessStatus(def.Name);
                return new
                {
                    name = def.Name,
                    displayName = def.DisplayName,
                    description = def.Description,
                    status = status.Status.ToString(),
                    statusText = status.DisplayText,
                    statusColor = status.Color,
                    processId = status.ProcessId,
                    canStart = status.Status is ProcessState.Stopped,
                    canStop = status.Status is ProcessState.Running or ProcessState.ExternalRunning,
                    needsInstall = status.Status == ProcessState.NotConfigured,
                };
            });
            return Results.Json(list);
        });

        app.MapPost("/api/lava/processes/{name}/start", async (string name) =>
        {
            var (ok, message) = await AppServices.ProcessManager.StartProcessAsync(name);
            return ok ? Results.Ok(new { ok, message }) : Results.BadRequest(new { error = message });
        });

        app.MapPost("/api/lava/processes/{name}/stop", async (string name) =>
        {
            var (ok, message) = await AppServices.ProcessManager.StopProcessAsync(name);
            return ok ? Results.Ok(new { ok, message }) : Results.BadRequest(new { error = message });
        });

        app.MapPost("/api/lava/open-apps-dir", () =>
        {
            var dir = AppServices.ProcessManager.GetAppsDirectory();
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = dir, UseShellExecute = true });
            return Results.Ok(new { ok = true, path = dir });
        });

        app.MapGet("/api/lava/data-info", () =>
        {
            DevEnv.Models.AppPaths.EnsureDirectories();
            var settings = AppServices.Config.Load();
            return Results.Json(new
            {
                root = DevEnv.Models.AppPaths.Root,
                configFile = DevEnv.Models.AppPaths.ConfigFile,
                cacheDir = string.IsNullOrWhiteSpace(settings.CacheDir) ? DevEnv.Models.AppPaths.CacheDir : settings.CacheDir,
                appsDir = string.IsNullOrWhiteSpace(settings.AppsDir) ? DevEnv.Models.AppPaths.AppsDir : settings.AppsDir,
                historyFile = DevEnv.Models.AppPaths.DownloadHistoryFile,
                settings = new
                {
                    autoExtractPortable = settings.AutoExtractPortable,
                    maxRetries = settings.MaxRetries,
                    timeout = settings.Timeout,
                    chunkSizeMb = settings.ChunkSizeMb,
                    useSystemProxy = settings.UseSystemProxy,
                    customProxy = settings.CustomProxy,
                },
                about = new
                {
                    productName = AppInfo.ProductName,
                    productShortName = AppInfo.ProductShortName,
                    tagline = AppInfo.ProductTagline,
                    version = AppInfo.Version,
                    docsUrl = AppInfo.DocsUrl,
                    repositoryUrl = AppInfo.RepositoryUrl,
                },
            });
        });

        app.MapPost("/api/lava/open-data-root", () =>
        {
            DevEnv.Models.AppPaths.EnsureDirectories();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = DevEnv.Models.AppPaths.Root,
                UseShellExecute = true,
            });
            return Results.Ok(new { ok = true, path = DevEnv.Models.AppPaths.Root });
        });

        app.MapPost("/api/lava/open-config", () =>
        {
            DevEnv.Models.AppPaths.EnsureDirectories();
            _ = AppServices.Config.Load();
            if (!File.Exists(DevEnv.Models.AppPaths.ConfigFile))
                return Results.BadRequest(new { error = "配置文件不存在" });
            Process.Start(new ProcessStartInfo
            {
                FileName = DevEnv.Models.AppPaths.ConfigFile,
                UseShellExecute = true,
            });
            return Results.Ok(new { ok = true, path = DevEnv.Models.AppPaths.ConfigFile });
        });

        app.MapPost("/api/lava/open-environment-variables", () =>
        {
            if (!WindowsToolRunner.TryRun("env", out var error))
                return Results.BadRequest(new { error = error ?? "无法打开环境变量" });
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/lava/scan", async () =>
        {
            var results = await AppServices.EnvironmentScanner.ScanAllAsync();
            return Results.Json(results);
        });
    }
}
