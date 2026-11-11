using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.NetworkWatch.Models;
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
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => RefreshSpeed();
        _timer.Start();
        RefreshSpeed();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(ShowsProgress))]
    [NotifyPropertyChangedFor(nameof(ShowsIdleHint))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionText))]
    private bool _speedRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PhaseLabel))]
    [NotifyPropertyChangedFor(nameof(ProgressPercentText))]
    [NotifyPropertyChangedFor(nameof(IsPingPhase))]
    [NotifyPropertyChangedFor(nameof(IsDownloadPhase))]
    [NotifyPropertyChangedFor(nameof(IsUploadPhase))]
    private SpeedTestProgressView? _speedProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(ShowsIdleHint))]
    [NotifyPropertyChangedFor(nameof(DownloadDisplay))]
    [NotifyPropertyChangedFor(nameof(UploadDisplay))]
    [NotifyPropertyChangedFor(nameof(PingDisplay))]
    [NotifyPropertyChangedFor(nameof(JitterDisplay))]
    [NotifyPropertyChangedFor(nameof(ServerSummary))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionText))]
    private SpeedTestResultView? _speedResult;

    [ObservableProperty]
    private string _speedError = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    public bool CanStart => !SpeedRunning;
    public bool CanCancel => SpeedRunning;
    public bool ShowsProgress => SpeedRunning;
    public bool HasResult => SpeedResult is not null;
    public bool ShowsIdleHint => !SpeedRunning && SpeedResult is null && string.IsNullOrWhiteSpace(SpeedError);

    public string PrimaryActionText => SpeedRunning
        ? "测速中…"
        : HasResult ? "重新测速" : "开始测速";

    public string DownloadDisplay => SpeedResult?.DownloadText ?? "—";
    public string UploadDisplay => SpeedResult?.UploadText ?? "—";
    public string PingDisplay => SpeedResult?.PingText ?? "—";
    public string JitterDisplay => SpeedResult?.JitterText ?? "—";

    public string ServerSummary
    {
        get
        {
            if (SpeedResult is null)
                return "使用 Cloudflare 等公共节点，测速约需 20～40 秒";
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(SpeedResult.ServerName))
                parts.Add(SpeedResult.ServerName);
            if (!string.IsNullOrWhiteSpace(SpeedResult.DurationText))
                parts.Add(SpeedResult.DurationText);
            if (!string.IsNullOrWhiteSpace(SpeedResult.UploadMessage))
                parts.Add(SpeedResult.UploadMessage);
            return string.Join(" · ", parts);
        }
    }

    public string PhaseLabel => PhaseOf(SpeedProgress) switch
    {
        SpeedTestPhase.Ping => "测量延迟",
        SpeedTestPhase.Downloading => "下载测速",
        SpeedTestPhase.Uploading => "上传测速",
        SpeedTestPhase.Completed => "测速完成",
        SpeedTestPhase.Cancelled => "已取消",
        SpeedTestPhase.Failed => "测速失败",
        _ => SpeedProgress?.Message ?? "准备中",
    };

    public string ProgressPercentText
    {
        get
        {
            if (SpeedProgress is null)
                return "0%";
            var pct = Math.Clamp(SpeedProgress.OverallPercent, 0, 1) * 100;
            return $"{pct:0}%";
        }
    }

    /// <summary>延迟阶段已开始或已完成（后续阶段保持点亮）。</summary>
    public bool IsPingPhase => PhaseOf(SpeedProgress) is SpeedTestPhase.Ping
        or SpeedTestPhase.Downloading or SpeedTestPhase.Uploading or SpeedTestPhase.Completed;

    /// <summary>下载阶段已开始或已完成。</summary>
    public bool IsDownloadPhase => PhaseOf(SpeedProgress) is SpeedTestPhase.Downloading
        or SpeedTestPhase.Uploading or SpeedTestPhase.Completed;

    /// <summary>上传阶段已开始或已完成。</summary>
    public bool IsUploadPhase => PhaseOf(SpeedProgress) is SpeedTestPhase.Uploading
        or SpeedTestPhase.Completed;

    private static SpeedTestPhase PhaseOf(SpeedTestProgressView? progress)
    {
        if (progress is null || string.IsNullOrWhiteSpace(progress.Phase))
            return SpeedTestPhase.Idle;
        return Enum.TryParse<SpeedTestPhase>(progress.Phase, ignoreCase: true, out var phase)
            ? phase
            : SpeedTestPhase.Idle;
    }

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
            HostServices.SetPageStatus(progress.Message ?? PhaseLabel, "speed");
        else if (status.Result is { } result)
            HostServices.SetPageStatus($"下载 {result.DownloadText} · 上传 {result.UploadText}", "speed");
        else
            HostServices.SetPageStatus(string.Empty, "speed");
    }

    [RelayCommand]
    public void StartSpeedTest()
    {
        if (SpeedRunning)
            return;

        Message = string.Empty;
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
