using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Host.SigXor.Services;

namespace DevEnv.Host.SigXor.ViewModels;

/// <summary>更新流程状态。</summary>
public enum UpdateFlowState
{
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToRestart,
    Error,
}

/// <summary>
/// 更新对话框 ViewModel：检查 → 下载（进度）→ 重启应用更新。
/// 底层是宿主的自研更新器（见 <see cref="UpdateService"/>），本类只负责呈现流程状态。
/// </summary>
public partial class UpdateViewModel : ObservableObject
{
    private const string ReleasesUrl = "https://github.com/pengcunfu/DevEnv/releases";

    private readonly UpdateService _updateService;

    [ObservableProperty]
    private UpdateFlowState _state = UpdateFlowState.Checking;

    [ObservableProperty]
    private string _message = "正在检查更新…";

    [ObservableProperty]
    private string? _releaseNotes;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string? _newVersion;

    public string CurrentVersion { get; } = AppVersion.Display;

    public UpdateViewModel(UpdateService updateService)
    {
        _updateService = updateService;
        _updateService.ProgressChanged += OnProgressChanged;
    }

    /// <summary>检查更新（窗口加载后自动触发）。</summary>
    [RelayCommand]
    private async Task CheckAsync()
    {
        if (!_updateService.IsUpdateCapable)
        {
            State = UpdateFlowState.Error;
            Message = "当前目录不支持自动更新（开发构建、只读目录或旧版 Velopack 安装）。\n"
                + "请前往 GitHub Releases 下载最新便携 zip：\n" + ReleasesUrl;
            return;
        }

        State = UpdateFlowState.Checking;
        Message = "正在检查更新…";
        try
        {
            bool hasUpdate = await _updateService.CheckForUpdatesAsync();
            if (!hasUpdate)
            {
                if (!string.IsNullOrWhiteSpace(_updateService.LastError))
                {
                    State = UpdateFlowState.Error;
                    Message = $"检查更新失败：{_updateService.LastError}";
                    return;
                }

                State = UpdateFlowState.UpToDate;
                Message = $"当前已是最新版本 v{CurrentVersion}";
                return;
            }

            NewVersion = _updateService.PendingVersion;
            State = UpdateFlowState.UpdateAvailable;
            Message = $"发现新版本 v{NewVersion}（当前 v{CurrentVersion}）";
        }
        catch (Exception ex)
        {
            State = UpdateFlowState.Error;
            Message = $"检查更新失败：{ex.Message}";
        }
    }

    /// <summary>下载更新并显示进度（0..100）。</summary>
    [RelayCommand]
    private void Download()
    {
        State = UpdateFlowState.Downloading;
        Message = "正在下载更新…";
        Progress = 0;

        var result = _updateService.StartDownload();
        if (!result.Ok)
        {
            State = UpdateFlowState.Error;
            Message = result.DisplayMessage;
        }
    }

    /// <summary>应用更新并重启；调用后当前进程将退出。</summary>
    [RelayCommand]
    private void Restart()
    {
        var result = _updateService.ApplyAndRestart();
        if (!result.Ok)
        {
            State = UpdateFlowState.Error;
            Message = result.DisplayMessage;
        }
    }

    private void OnProgressChanged(int percent)
    {
        Progress = percent;
        Message = $"正在下载更新 {percent}%";
        if (percent >= 100)
        {
            State = UpdateFlowState.ReadyToRestart;
            Message = "更新已下载完成，重启后生效。";
        }
    }
}
