using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Velopack;
using Velopack.Sources;

namespace DevBox.Api;

public static class UpdateEndpoints
{
    private const string GithubRepo = "https://github.com/pengcunfu/DevBox.Net";
    private const string FeedEnvVar = "DEVBOX_UPDATE_FEED";

    private static UpdateManager CreateManager()
    {
        var overrideFeed = Environment.GetEnvironmentVariable(FeedEnvVar);
        return string.IsNullOrWhiteSpace(overrideFeed)
            ? new UpdateManager(new GithubSource(GithubRepo, accessToken: null, prerelease: false))
            : new UpdateManager(overrideFeed);
    }

    private static UpdateInfo? _pending;
    private static int _downloadProgress;
    private static bool _downloading;
    private static string? _lastError;

    public static void MapUpdateEndpoints(this WebApplication app)
    {
        app.MapGet("/api/update/status", () =>
        {
            var mgr = CreateManager();
            return Results.Json(new
            {
                isInstalled = mgr.IsInstalled,
                currentVersion = mgr.CurrentVersion?.ToString()
                    ?? typeof(DevBoxApiHost).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                pendingVersion = _pending?.TargetFullRelease.Version.ToString(),
                downloading = _downloading,
                downloadProgress = _downloadProgress,
                error = _lastError,
            });
        });

        app.MapPost("/api/update/check", async () =>
        {
            try
            {
                _lastError = null;
                var mgr = CreateManager();
                if (!mgr.IsInstalled)
                    return Results.Ok(new { updateAvailable = false, message = "当前为开发/非安装态，无法通过 Velopack 更新" });

                _pending = await mgr.CheckForUpdatesAsync();
                if (_pending is null)
                    return Results.Ok(new { updateAvailable = false, message = "已是最新版本" });

                return Results.Ok(new
                {
                    updateAvailable = true,
                    version = _pending.TargetFullRelease.Version.ToString(),
                    message = $"发现新版本 {_pending.TargetFullRelease.Version}",
                });
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/update/download", () =>
        {
            if (_pending is null)
                return Results.BadRequest(new { error = "请先检查更新" });
            if (_downloading)
                return Results.BadRequest(new { error = "正在下载" });

            var info = _pending;
            _downloading = true;
            _downloadProgress = 0;
            _lastError = null;
            _ = Task.Run(async () =>
            {
                try
                {
                    var mgr = CreateManager();
                    await mgr.DownloadUpdatesAsync(info, p => _downloadProgress = p);
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                }
                finally
                {
                    _downloading = false;
                }
            });

            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/update/apply", () =>
        {
            if (_pending is null)
                return Results.BadRequest(new { error = "没有待应用的更新" });
            try
            {
                var mgr = CreateManager();
                HostCallbacks.OnBeforeExitForUpdate?.Invoke();
                mgr.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
                return Results.Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}
