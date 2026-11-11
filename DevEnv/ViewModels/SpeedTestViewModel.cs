using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>网络测速：公共节点测延迟 / 下载 / 上传。</summary>
public sealed partial class SpeedTestViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public SpeedTestViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => RefreshSpeed();
        _timer.Start();
        RefreshSpeed();
    }

    [ObservableProperty]
    private bool _speedRunning;

    [ObservableProperty]
    private SpeedTestProgressView? _speedProgress;

    [ObservableProperty]
    private SpeedTestResultView? _speedResult;

    [ObservableProperty]
    private string _speedError = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    private void RefreshSpeed()
    {
        if (_disposed)
            return;

        var status = CoreServices.Network.GetSpeedTestStatus();
        SpeedRunning = status.Running;
        SpeedProgress = status.Progress;
        SpeedResult = status.Result;
        SpeedError = status.Error ?? string.Empty;

        if (status.Running && status.Progress is { } progress)
            HostServices.SetPageStatus(progress.Message ?? "测速中…", "speed");
        else if (status.Result is { } result)
            HostServices.SetPageStatus($"下载 {result.DownloadText} · 上传 {result.UploadText}", "speed");
    }

    [RelayCommand]
    public void StartSpeedTest()
    {
        var result = CoreServices.Network.StartSpeedTest();
        if (!result.Ok)
            Message = result.DisplayMessage;
        RefreshSpeed();
    }

    [RelayCommand]
    public void CancelSpeedTest()
    {
        CoreServices.Network.CancelSpeedTest();
        RefreshSpeed();
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        HostServices.SetPageStatus(string.Empty, "speed");
    }
}
