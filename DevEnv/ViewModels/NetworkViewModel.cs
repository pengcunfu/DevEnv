using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>网络监控页：流量（按进程）/ 端口 / 测速三个页签。</summary>
public sealed partial class NetworkViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _monitorTimer;
    private readonly DispatcherTimer _portTimer;
    private readonly DispatcherTimer _speedTimer;

    public NetworkViewModel()
    {
        Processes = [];
        Ports = [];

        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _monitorTimer.Tick += (_, _) => { if (!Paused && SelectedTab == "monitor") RefreshSnapshot(); };

        _portTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _portTimer.Tick += (_, _) => { if (SelectedTab == "ports") RefreshPorts(); };

        _speedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _speedTimer.Tick += (_, _) => { if (SelectedTab == "speed") RefreshSpeed(); };

        _monitorTimer.Start();
        _portTimer.Start();
        _speedTimer.Start();
        RefreshSnapshot();
    }

    #region 公共

    public IReadOnlyList<NavTab> Tabs { get; } =
    [
        new NavTab { Id = "monitor", Title = "流量" },
        new NavTab { Id = "ports", Title = "端口" },
        new NavTab { Id = "speed", Title = "测速" },
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedConnections))]
    private string _selectedTab = "monitor";

    [ObservableProperty]
    private bool _paused;

    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>切页签立即拉一次数据，不用等定时器那一拍。</summary>
    public void OnTabChanged(int index)
    {
        SelectedTab = index >= 0 && index < Tabs.Count ? Tabs[index].Id : "monitor";
        switch (SelectedTab)
        {
            case "ports": RefreshPorts(); break;
            case "speed": RefreshSpeed(); break;
            default: RefreshSnapshot(); break;
        }
    }

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

    public IEnumerable<NetworkConnectionView> SelectedConnections =>
        SelectedProcess?.Connections ?? [];

    partial void OnQueryChanged(string value) => ApplyFilter();

    partial void OnSelectedProcessChanged(NetworkProcessView? value) => OnPropertyChanged(nameof(SelectedConnections));

    private IEnumerable<NetworkProcessView> _all = [];

    [RelayCommand]
    public void RefreshSnapshot()
    {
        var snapshot = CoreServices.Network.GetSnapshot();
        _all = snapshot.Processes;
        TotalConnections = snapshot.TotalConnections;
        TotalDownload = snapshot.TotalDownloadText;
        TotalUpload = snapshot.TotalUploadText;
        IsElevated = snapshot.IsElevated;
        StatusHint = snapshot.StatusHint ?? string.Empty;
        ApplyFilter();
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
        var prompt = asAdmin ? "将以管理员身份重启燧岚开发盒子？" : "将以降权普通用户重启？";
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner, prompt, "重启"))
            return;
        var result = CoreServices.Network.RestartElevated(asAdmin);
        if (!result.Ok)
            Message = result.DisplayMessage;
    }

    #endregion

    #region 端口

    public ObservableCollection<PortRowView> Ports { get; }

    [ObservableProperty]
    private string _portQuery = string.Empty;

    public IEnumerable<PortRowView> FilteredPorts
    {
        get
        {
            var query = PortQuery.Trim();
            return string.IsNullOrEmpty(query)
                ? Ports
                : Ports.Where(p =>
                    p.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || p.ProcessId.ToString().Contains(query, StringComparison.Ordinal)
                    || p.LocalPort.ToString().Contains(query, StringComparison.Ordinal)
                    || p.LocalAddress.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    partial void OnPortQueryChanged(string value) => OnPropertyChanged(nameof(FilteredPorts));

    [RelayCommand]
    public void RefreshPorts()
    {
        Ports.Clear();
        foreach (var port in CoreServices.Network.GetPorts())
            Ports.Add(port);
        OnPropertyChanged(nameof(FilteredPorts));
    }

    [RelayCommand]
    private void KillPortProcess(PortRowView? row)
    {
        if (row is null || row.ProcessId <= 0)
            return;
        if (!Windows.ConfirmDialog.Show(HostServices.DialogOwner,
                $"确定结束占用该连接的进程「{row.ProcessName}」(PID {row.ProcessId})？", "结束"))
            return;

        var result = CoreServices.Network.KillPortProcess(row.ProcessId);
        if (!result.Ok)
            Message = result.DisplayMessage;
        RefreshPorts();
    }

    #endregion

    #region 测速

    [ObservableProperty]
    private bool _speedRunning;

    [ObservableProperty]
    private SpeedTestProgressView? _speedProgress;

    [ObservableProperty]
    private SpeedTestResultView? _speedResult;

    [ObservableProperty]
    private string _speedError = string.Empty;

    private void RefreshSpeed()
    {
        var status = CoreServices.Network.GetSpeedTestStatus();
        SpeedRunning = status.Running;
        SpeedProgress = status.Progress;
        SpeedResult = status.Result;
        SpeedError = status.Error ?? string.Empty;
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

    #endregion

    public void Dispose()
    {
        _monitorTimer.Stop();
        _portTimer.Stop();
        _speedTimer.Stop();
    }
}