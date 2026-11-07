using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>「关于与帮助」页：产品信息 + 检查更新。更新状态与下载进度从宿主更新服务读取。</summary>
public sealed partial class AboutViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _updateTimer;

    public AboutViewModel()
    {
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _updateTimer.Tick += (_, _) =>
        {
            if (Update?.Downloading != true)
                return;
            Update = CoreServices.Update.GetStatus();
            if (Update.Downloading || Update.DownloadProgress < 100)
                return;
            _updateTimer.Stop();
        };

        Update = CoreServices.Update.GetStatus();
    }

    public string VersionText { get; } = $"版本 {HostServices.VersionText}";

    [ObservableProperty]
    private UpdateStatusView? _update;

    [ObservableProperty]
    private string _updateMessage = string.Empty;

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        var result = await CoreServices.Update.CheckAsync();
        UpdateMessage = result.DisplayMessage;
        Update = CoreServices.Update.GetStatus();
    }

    [RelayCommand]
    private void DownloadUpdate()
    {
        var result = CoreServices.Update.StartDownload();
        UpdateMessage = result.Ok ? "开始下载…" : result.DisplayMessage;
        _updateTimer.Start();
        Update = CoreServices.Update.GetStatus();
    }

    [RelayCommand]
    private void ApplyUpdate()
    {
        var result = CoreServices.Update.ApplyAndRestart();
        UpdateMessage = result.DisplayMessage;
    }

    public void Dispose() => _updateTimer.Stop();
}
