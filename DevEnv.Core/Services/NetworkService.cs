using System.Diagnostics;
using DevEnv.Core.Models;
using DevEnv.Core.LavaEnv.Services;
using DevEnv.Core.NetworkWatch.Models;
using DevEnv.Core.NetworkWatch.Services;

namespace DevEnv.Core.Services;

/// <summary>
/// 网络监控：按进程的连接流量、端口管理、一键测速。
/// 逻辑来自 <c>NetworkEndpoints</c> / <c>NetworkExtraEndpoints</c>，测速状态机（静态字段）原样搬到这里。
/// </summary>
public sealed class NetworkService
{
    private readonly NetworkMonitorService _monitor = new() { SortByTraffic = true };
    private readonly PortManagerService _ports = new();

    private SpeedTestService? _speed;
    private CancellationTokenSource? _speedCts;
    private SpeedTestProgress? _lastProgress;
    private SpeedTestResult? _lastResult;
    private string? _lastError;
    private volatile bool _running;

    public NetworkService() => _monitor.Start();

    public NetworkSnapshotView GetSnapshot(bool sortByTraffic = true)
    {
        _monitor.SortByTraffic = sortByTraffic;
        var snapshot = _monitor.CollectSnapshot();
        return new NetworkSnapshotView
        {
            Timestamp = snapshot.Timestamp,
            TotalConnections = snapshot.TotalConnections,
            TotalDownloadText = TrafficFormatter.FormatRate(snapshot.TotalDownloadRate),
            TotalUploadText = TrafficFormatter.FormatRate(snapshot.TotalUploadRate),
            IsElevated = snapshot.IsElevated,
            StatusHint = snapshot.StatusHint,
            Processes = snapshot.Processes.Select(p => new NetworkProcessView
            {
                ProcessId = p.ProcessId,
                DisplayName = p.DisplayName,
                ProcessName = p.ProcessName,
                ExecutablePath = p.ExecutablePath,
                ConnectionCount = p.ConnectionCount,
                DownloadRateText = p.DownloadRateText,
                UploadRateText = p.UploadRateText,
                Connections = p.Connections.Select(c => new NetworkConnectionView
                {
                    Protocol = c.Protocol,
                    LocalEndpoint = c.LocalEndpoint,
                    RemoteEndpoint = c.RemoteEndpoint,
                    State = c.State,
                    DownloadRateText = c.DownloadRateText,
                    UploadRateText = c.UploadRateText,
                }).ToList(),
            }).ToList(),
        };
    }

    public IReadOnlyList<PortRowView> GetPorts() =>
        _ports.GetConnections().Select(c => new PortRowView
        {
            Protocol = c.Protocol,
            LocalAddress = c.LocalAddress,
            LocalPort = c.LocalPort,
            RemoteAddress = c.RemoteAddress,
            RemotePort = c.RemotePort,
            State = c.State,
            ProcessId = c.ProcessId,
            ProcessName = c.ProcessName,
            LocalEndpoint = c.LocalEndpoint,
            RemoteEndpoint = c.RemoteEndpoint,
        }).ToList();

    public OperationResult KillPortProcess(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            proc.Kill(entireProcessTree: false);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public bool IsElevated => ElevationHelper.IsAdministrator();

    public OperationResult RestartElevated(bool asAdmin)
    {
        if (HostCallbacks.RestartElevated is null)
            return OperationResult.Fail("宿主未接管重启回调");
        HostCallbacks.RestartElevated(asAdmin);
        return OperationResult.Success(asAdmin ? "正在以管理员身份重启" : "正在降权重启");
    }

    public SpeedTestStatusView GetSpeedTestStatus() => new()
    {
        Running = _running,
        Progress = _lastProgress is null
            ? null
            : new SpeedTestProgressView
            {
                Phase = _lastProgress.Phase.ToString(),
                Percent = _lastProgress.Percent,
                OverallPercent = _lastProgress.OverallPercent,
                CurrentSpeedText = TrafficFormatter.FormatRate(_lastProgress.CurrentSpeed),
                Message = _lastProgress.Message,
            },
        Result = _lastResult is null
            ? null
            : new SpeedTestResultView
            {
                DownloadText = _lastResult.DownloadText,
                UploadText = _lastResult.UploadText,
                PingText = _lastResult.PingText,
                JitterText = _lastResult.JitterText,
                ServerName = _lastResult.ServerName,
                DurationText = _lastResult.DurationText,
                UploadMessage = _lastResult.UploadMessage,
            },
        Error = _lastError,
    };

    public OperationResult StartSpeedTest()
    {
        if (_running)
            return OperationResult.Fail("测速进行中");

        _running = true;
        _lastError = null;
        _lastResult = null;
        _lastProgress = null;
        _speedCts = new CancellationTokenSource();
        _speed = new SpeedTestService();
        var token = _speedCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                _lastResult = await _speed!.RunAsync(token, p => _lastProgress = p);
            }
            catch (OperationCanceledException)
            {
                _lastError = "已取消";
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
            }
            finally
            {
                _running = false;
                _speed?.Dispose();
                _speed = null;
            }
        }, token);

        return OperationResult.Success();
    }

    public void CancelSpeedTest() => _speedCts?.Cancel();
}