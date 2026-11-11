using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;
using DevEnv.Host.Windows;

namespace DevEnv.Host.ViewModels;

/// <summary>端口管理：列出本机连接，可按端口过滤、查看进程详情并结束占用进程。</summary>
public sealed partial class PortsViewModel : ObservableObject, IDisposable
{
    private const string PageId = "ports";
    private readonly DispatcherTimer _timer;
    private int _refreshing;
    private bool _disposed;

    public PortsViewModel()
    {
        Ports = [];
        IsElevated = CoreServices.Network.IsElevated;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => _ = RefreshPorts();
        if (AutoRefresh)
            _timer.Start();
        _ = RefreshPorts();
    }

    public ObservableCollection<PortRowView> Ports { get; }

    [ObservableProperty]
    private string _portQuery = string.Empty;

    [ObservableProperty]
    private PortRowView? _selected;

    [ObservableProperty]
    private bool _autoRefresh = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElevationActionText))]
    private bool _isElevated;

    public string ElevationActionText => IsElevated ? "降为普通权限" : "请求管理员权限";

    public IEnumerable<PortRowView> FilteredPorts
    {
        get
        {
            var query = PortQuery.Trim();
            return string.IsNullOrEmpty(query)
                ? Ports
                : Ports.Where(p =>
                    p.LocalPort.ToString().Contains(query, StringComparison.Ordinal)
                    || p.ProcessId.ToString().Contains(query, StringComparison.Ordinal)
                    || p.LocalAddress.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || p.RemoteEndpoint.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || p.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    partial void OnPortQueryChanged(string value) => OnPropertyChanged(nameof(FilteredPorts));

    partial void OnAutoRefreshChanged(bool value)
    {
        if (_disposed)
            return;
        if (value)
            _timer.Start();
        else
            _timer.Stop();
    }

    [RelayCommand]
    public async Task RefreshPorts()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
            return;

        try
        {
            var ports = await Task.Run(() => CoreServices.Network.GetPorts());
            if (_disposed)
                return;

            (string Protocol, string LocalAddress, int LocalPort, string RemoteEndpoint, int ProcessId)? keep = Selected is null
                ? null
                : (Selected.Protocol, Selected.LocalAddress, Selected.LocalPort, Selected.RemoteEndpoint, Selected.ProcessId);

            Ports.Clear();
            foreach (var port in ports)
                Ports.Add(port);
            OnPropertyChanged(nameof(FilteredPorts));

            Selected = keep is null
                ? null
                : FilteredPorts.FirstOrDefault(p =>
                    p.Protocol == keep.Value.Protocol
                    && p.LocalAddress == keep.Value.LocalAddress
                    && p.LocalPort == keep.Value.LocalPort
                    && p.RemoteEndpoint == keep.Value.RemoteEndpoint
                    && p.ProcessId == keep.Value.ProcessId);

            IsElevated = CoreServices.Network.IsElevated;
            HostServices.SetPageStatus(
                $"端口 {Ports.Count} · {(IsElevated ? "管理员" : "普通权限")}"
                + (AutoRefresh ? " · 自动刷新" : string.Empty),
                PageId);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    [RelayCommand]
    private async Task ShowDetail()
    {
        if (Selected is null || Selected.ProcessId <= 0)
        {
            HostServices.SetStatus("请先选择一条连接", PageId);
            return;
        }

        var row = Selected;
        var detail = await Task.Run(() => CoreServices.Network.GetPortProcessDetail(row));
        if (_disposed)
            return;

        PortProcessDetailWindow.Show(HostServices.DialogOwner, detail);
    }

    [RelayCommand]
    private void KillPortProcess()
    {
        if (Selected is null || Selected.ProcessId <= 0)
        {
            HostServices.SetStatus("请先选择一条连接", PageId);
            return;
        }

        var name = string.IsNullOrWhiteSpace(Selected.ProcessName)
            ? $"PID {Selected.ProcessId}"
            : $"{Selected.ProcessName} (PID {Selected.ProcessId})";
        if (!ConfirmDialog.Show(HostServices.DialogOwner,
                $"确定终止进程「{name}」？\n占用端口 {Selected.LocalPort} 的连接也会断开。", "终止进程"))
            return;

        var result = CoreServices.Network.KillPortProcess(Selected.ProcessId);
        HostServices.SetStatus(
            result.Ok ? $"已终止 {name}" : result.DisplayMessage,
            PageId);
        _ = RefreshPorts();
    }

    [RelayCommand]
    private void RestartElevated()
    {
        var asAdmin = !IsElevated;
        var prompt = asAdmin
            ? "将以管理员身份重启 FireNeb DevEnv？\n提权后可查看更多进程信息并终止受保护进程。"
            : "将以降权普通用户重启？";
        if (!ConfirmDialog.Show(HostServices.DialogOwner, prompt, "重启"))
            return;

        var result = CoreServices.Network.RestartElevated(asAdmin);
        if (!result.Ok)
            HostServices.SetStatus(result.DisplayMessage, PageId);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        HostServices.SetPageStatus(string.Empty, PageId);
    }
}
