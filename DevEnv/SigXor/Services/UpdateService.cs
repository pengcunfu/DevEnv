using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevEnv.Core.Models;

namespace DevEnv.Host.SigXor.Services;

/// <summary>
/// 模块的更新检查入口。
///
/// 原实现是 SigXor 独立应用的自研更新器（自建 ZIP 分发 + 看门狗回滚），
/// 并入 DevEnv 后与宿主的 Velopack 自更新重复，属于第二套基础设施
/// （见 AGENTS.md「不要再引入第二套基础设施」），因此改为包装宿主的
/// <see cref="Core.Services.UpdateService"/>：
///
/// - 检查 / 下载 / 应用重启全部走 Velopack，发布源与主程序一致；
/// - 更新窗口仍保留在本模块内，供「关于」窗口的「检查更新」入口调用；
/// - 模块不再自行下载或替换 exe，也不再维护 .bak 回滚逻辑。
/// </summary>
public sealed class UpdateService
{
    // 注意：必须写全 DevEnv.Core.Services.UpdateService。
    // 本文件所在命名空间 DevEnv.Host.SigXor.Services 里能解析到 DevEnv.Core.SigXor，
    // 简写 Core.Services.UpdateService 会被它遮蔽。
    private readonly DevEnv.Core.Services.UpdateService _host;

    public UpdateService()
    {
        _host = new DevEnv.Core.Services.UpdateService();
    }

    /// <summary>当前环境是否支持自动更新（Velopack 安装态）。</summary>
    public bool IsUpdateCapable
    {
        get
        {
            try { return _host.GetStatus().IsInstalled; }
            catch { return false; }
        }
    }

    /// <summary>检查更新。返回有新版时为 true。</summary>
    public async Task<bool> CheckForUpdatesAsync()
    {
        var result = await _host.CheckAsync();
        return result.Ok && _host.GetStatus().PendingVersion is not null;
    }

    /// <summary>当前版本号。</summary>
    public string CurrentVersion => _host.GetStatus().CurrentVersion ?? "0.0.0";

    /// <summary>下载进度回调（0..100）。</summary>
    public event Action<int>? ProgressChanged;

    /// <summary>开始下载更新（后台）。</summary>
    public OperationResult StartDownload()
    {
        var result = _host.StartDownload();
        if (!result.Ok) return result;

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var status = _host.GetStatus();
                ProgressChanged?.Invoke(status.DownloadProgress);
                if (!status.Downloading) break;
                await Task.Delay(300);
            }
        });

        return OperationResult.Success();
    }

    /// <summary>应用更新并重启宿主。</summary>
    public OperationResult ApplyAndRestart() => _host.ApplyAndRestart();
}
