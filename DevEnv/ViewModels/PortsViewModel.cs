using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Models;
using DevEnv.Core.Services;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

/// <summary>端口管理：列出本机连接，可按端口 / 进程过滤并结束占用进程。</summary>
public sealed partial class PortsViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private int _refreshing;
    private bool _disposed;

    public PortsViewModel()
    {
        Ports = [];
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => _ = RefreshPorts();
        _timer.Start();
        _ = RefreshPorts();
    }

    public ObservableCollection<PortRowView> Ports { get; }

    [ObservableProperty]
    private string _portQuery = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

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
    public async Task RefreshPorts()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
            return;

        try
        {
            var ports = await Task.Run(() => CoreServices.Network.GetPorts());
            if (_disposed)
                return;

            Ports.Clear();
            foreach (var port in ports)
                Ports.Add(port);
            OnPropertyChanged(nameof(FilteredPorts));
            HostServices.SetPageStatus($"端口 {Ports.Count}", "ports");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
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
        _ = RefreshPorts();
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        HostServices.SetPageStatus(string.Empty, "ports");
    }
}
