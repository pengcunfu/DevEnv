using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NetworkWatch.Models;
using NetworkWatch.Services;

namespace DevBox.Api;

public static class NetworkEndpoints
{
    private static NetworkMonitorService? _monitor;

    public static void MapNetworkEndpoints(this WebApplication app)
    {
        _monitor ??= new NetworkMonitorService { SortByTraffic = true };
        _monitor.Start();

        app.MapGet("/api/network/snapshot", (bool? sortByTraffic) =>
        {
            _monitor!.SortByTraffic = sortByTraffic != false;
            var snapshot = _monitor.CollectSnapshot();
            return Results.Json(ToDto(snapshot));
        });
    }

    private static object ToDto(MonitorSnapshot snapshot) => new
    {
        timestamp = snapshot.Timestamp,
        totalConnections = snapshot.TotalConnections,
        totalDownloadRate = snapshot.TotalDownloadRate,
        totalUploadRate = snapshot.TotalUploadRate,
        totalDownloadText = TrafficFormatter.FormatRate(snapshot.TotalDownloadRate),
        totalUploadText = TrafficFormatter.FormatRate(snapshot.TotalUploadRate),
        isElevated = snapshot.IsElevated,
        statusHint = snapshot.StatusHint,
        trafficStatsSuccessCount = snapshot.TrafficStatsSuccessCount,
        trafficStatsEligibleCount = snapshot.TrafficStatsEligibleCount,
        processes = snapshot.Processes.Select(p => new
        {
            processId = p.ProcessId,
            displayName = p.DisplayName,
            processName = p.ProcessName,
            executablePath = p.ExecutablePath,
            connectionCount = p.ConnectionCount,
            downloadRateText = p.DownloadRateText,
            uploadRateText = p.UploadRateText,
            connections = p.Connections.Select(c => new
            {
                protocol = c.Protocol,
                localEndpoint = c.LocalEndpoint,
                remoteEndpoint = c.RemoteEndpoint,
                state = c.State,
                downloadRateText = c.DownloadRateText,
                uploadRateText = c.UploadRateText,
            }),
        }),
    };
}
