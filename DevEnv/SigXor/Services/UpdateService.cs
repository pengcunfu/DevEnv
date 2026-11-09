using System.Threading.Tasks;
using DevEnv.Core.Models;
using DevEnv.Core.Services;

namespace DevEnv.Host.SigXor.Services;

/// <summary>
/// 模块的更新检查入口。更新逻辑统一走宿主的 <see cref="Core.Services.UpdateService"/> 单例，
/// 不再自己 new 一份状态，避免「关于页」与模块更新窗显示不同结果。
/// </summary>
public sealed class UpdateService
{
    private DevEnv.Core.Services.UpdateService Host => CoreServices.Update;

    /// <summary>当前环境是否支持自动更新（便携版且目录可写）。</summary>
    public bool IsUpdateCapable
    {
        get
        {
            try { return Host.GetStatus().CanUpdate; }
            catch { return false; }
        }
    }

    /// <summary>上次检查失败的原因；成功或已是最新时为 null。</summary>
    public string? LastError { get; private set; }

    /// <summary>检查更新。返回有新版时为 true；失败时 <see cref="LastError"/> 有值。</summary>
    public async Task<bool> CheckForUpdatesAsync()
    {
        OperationResult result = await Host.CheckAsync();
        LastError = result.Ok ? null : result.DisplayMessage;
        return result.Ok && Host.GetStatus().PendingVersion is not null;
    }

    public string CurrentVersion => Host.GetStatus().CurrentVersion;

    public string? PendingVersion => Host.GetStatus().PendingVersion;

    /// <summary>下载进度回调（0..100）。</summary>
    public event Action<int>? ProgressChanged;

    /// <summary>开始下载更新（后台）。</summary>
    public OperationResult StartDownload()
    {
        OperationResult result = Host.StartDownload();
        if (!result.Ok) return result;

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var status = Host.GetStatus();
                ProgressChanged?.Invoke(status.DownloadProgress);
                if (!status.Downloading) break;
                await Task.Delay(300);
            }
        });

        return OperationResult.Success();
    }

    /// <summary>应用更新并重启宿主。</summary>
    public OperationResult ApplyAndRestart() => Host.ApplyAndRestart();
}
