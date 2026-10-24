using DevEnv.Core.Models;
using Velopack;
using Velopack.Sources;

namespace DevEnv.Core.Services;

/// <summary>
/// Velopack 自更新状态机（检查 → 下载 → 应用重启）。
/// 逻辑来自 <c>UpdateEndpoints</c>，静态字段改为实例状态。
/// </summary>
public sealed class UpdateService
{
    private const string GithubRepo = "https://github.com/pengcunfu/DevEnv";
    private const string FeedEnvVar = "DEVBOX_UPDATE_FEED";

    private UpdateInfo? _pending;
    private int _downloadProgress;
    private volatile bool _downloading;
    private string? _lastError;

    private static UpdateManager CreateManager()
    {
        var overrideFeed = Environment.GetEnvironmentVariable(FeedEnvVar);
        return string.IsNullOrWhiteSpace(overrideFeed)
            ? new UpdateManager(new GithubSource(GithubRepo, accessToken: null, prerelease: false))
            : new UpdateManager(overrideFeed);
    }

    public UpdateStatusView GetStatus() => new()
    {
        IsInstalled = SafeIsInstalled(),
        CurrentVersion = SafeCurrentVersion(),
        PendingVersion = _pending?.TargetFullRelease.Version.ToString(),
        Downloading = _downloading,
        DownloadProgress = _downloadProgress,
        Error = _lastError,
    };

    public async Task<OperationResult> CheckAsync()
    {
        try
        {
            _lastError = null;
            var mgr = CreateManager();
            if (!mgr.IsInstalled)
                return OperationResult.Success("当前为开发/非安装态，无法通过 Velopack 更新");

            _pending = await mgr.CheckForUpdatesAsync();
            if (_pending is null)
                return OperationResult.Success("已是最新版本");

            return OperationResult.Success($"发现新版本 {_pending.TargetFullRelease.Version}");
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult StartDownload()
    {
        if (_pending is null)
            return OperationResult.Fail("请先检查更新");
        if (_downloading)
            return OperationResult.Fail("正在下载");

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

        return OperationResult.Success();
    }

    public OperationResult ApplyAndRestart()
    {
        if (_pending is null)
            return OperationResult.Fail("没有待应用的更新");

        try
        {
            var mgr = CreateManager();
            HostCallbacks.OnBeforeExitForUpdate?.Invoke();
            mgr.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
            return OperationResult.Success("正在应用更新并重启");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private static bool SafeIsInstalled()
    {
        try { return CreateManager().IsInstalled; }
        catch { return false; }
    }

    private static string SafeCurrentVersion()
    {
        try
        {
            return CreateManager().CurrentVersion?.ToString()
                ?? typeof(UpdateService).Assembly.GetName().Version?.ToString()
                ?? "0.0.0";
        }
        catch
        {
            return typeof(UpdateService).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        }
    }
}