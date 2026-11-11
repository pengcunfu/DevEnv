using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>网络监控：按进程展示 TCP/UDP 连接与上下行速率。</summary>
public sealed partial class NetworkViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _monitorTimer;

    private int _snapshotRefreshing;
    private bool _disposed;

    public NetworkViewModel()
    {
        Processes = [];

        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _monitorTimer.Tick += (_, _) => { if (!Paused) _ = RefreshSnapshot(); };
        _monitorTimer.Start();
        _ = RefreshSnapshot();
    }

    #region 公共

    [ObservableProperty]
    private bool _paused;

    [ObservableProperty]
    private string _message = string.Empty;

    #endregion

    #region 流量

    public ObservableCollection<NetworkProcessView> Processes { get; }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private NetworkProcessView? _selectedProcess;

    [ObservableProperty]
    private int _totalConnections;

    [ObservableProperty]
    private string _totalDownload = "—";

    [ObservableProperty]
    private string _totalUpload = "—";

    [ObservableProperty]
    private bool _isElevated;

    [ObservableProperty]
    private string _statusHint = string.Empty;

    partial void OnQueryChanged(string value) => ApplyFilter();

    private IEnumerable<NetworkProcessView> _all = [];

    /// <summary>采样放到线程池，避免每秒一次的连接枚举阻塞界面。</summary>
    [RelayCommand]
    public async Task RefreshSnapshot()
    {
        if (Interlocked.Exchange(ref _snapshotRefreshing, 1) == 1)
            return;

        try
        {
            var snapshot = await Task.Run(() => CoreServices.Network.GetSnapshot());
            if (_disposed)
                return;

            _all = snapshot.Processes;
            TotalConnections = snapshot.TotalConnections;
            TotalDownload = snapshot.TotalDownloadText;
            TotalUpload = snapshot.TotalUploadText;
            IsElevated = snapshot.IsElevated;
            StatusHint = snapshot.StatusHint ?? string.Empty;
            ApplyFilter();
            PublishStatus();
        }
        finally
        {
            Interlocked.Exchange(ref _snapshotRefreshing, 0);
        }
    }

    private void PublishStatus()
    {
        var summary = $"连接 {TotalConnections} · 下载 {TotalDownload} · 上传 {TotalUpload}";
        if (!string.IsNullOrWhiteSpace(StatusHint))
            summary += $" · {StatusHint}";
        HostServices.SetPageStatus(summary, "network");
    }

    private void ApplyFilter()
    {
        var query = Query.Trim();
        var rows = string.IsNullOrEmpty(query)
            ? _all
            : _all.Where(p =>
                p.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || p.ProcessId.ToString().Contains(query, StringComparison.Ordinal)
                || (p.ExecutablePath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        var selectedId = SelectedProcess?.ProcessId;
        Processes.Clear();
        foreach (var row in rows)
            Processes.Add(row);
        SelectedProcess = selectedId is null ? null : Processes.FirstOrDefault(p => p.ProcessId == selectedId);
    }

    [RelayCommand]
    private void TogglePause() => Paused = !Paused;

    [RelayCommand]
    private void RestartElevated()
    {
        var asAdmin = !IsElevated;
        var prompt = asAdmin ? "将以管理员身份重启FireNeb DevEnv？" : "将以降权普通用户重启？";
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, prompt, "重启"))
            return;
        var result = CoreServices.Network.RestartElevated(asAdmin);
        if (!result.Ok)
            Message = result.DisplayMessage;
    }

    #endregion

    public void Dispose()
    {
        _disposed = true;
        _monitorTimer.Stop();
        HostServices.SetPageStatus(string.Empty, "network");
    }
}
