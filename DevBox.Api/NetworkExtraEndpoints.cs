using System.Diagnostics;
using DevEnv.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NetworkWatch.Models;
using NetworkWatch.Services;

namespace DevBox.Api;

public static class NetworkExtraEndpoints
{
    private static readonly PortManagerService Ports = new();
    private static SpeedTestService? _speed;
    private static CancellationTokenSource? _speedCts;
    private static SpeedTestProgress? _lastProgress;
    private static SpeedTestResult? _lastResult;
    private static string? _lastError;
    private static bool _running;

    public static void MapNetworkExtraEndpoints(this WebApplication app)
    {
        app.MapGet("/api/network/ports", () =>
        {
            var list = Ports.GetConnections().Select(c => new
            {
                protocol = c.Protocol,
                localAddress = c.LocalAddress,
                localPort = c.LocalPort,
                remoteAddress = c.RemoteAddress,
                remotePort = c.RemotePort,
                state = c.State,
                processId = c.ProcessId,
                processName = c.ProcessName,
            });
            return Results.Json(list);
        });

        app.MapPost("/api/network/ports/{pid:int}/kill", (int pid) =>
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill(entireProcessTree: false);
                return Results.Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/network/speedtest/status", () => Results.Json(new
        {
            running = _running,
            progress = _lastProgress is null ? null : new
            {
                phase = _lastProgress.Phase.ToString(),
                percent = _lastProgress.Percent,
                overallPercent = _lastProgress.OverallPercent,
                bytesTransferred = _lastProgress.BytesTransferred,
                currentSpeed = _lastProgress.CurrentSpeed,
                currentSpeedText = TrafficFormatter.FormatRate(_lastProgress.CurrentSpeed),
                message = _lastProgress.Message,
            },
            result = _lastResult is null ? null : new
            {
                downloadText = _lastResult.DownloadText,
                uploadText = _lastResult.UploadText,
                pingText = _lastResult.PingText,
                jitterText = _lastResult.JitterText,
                serverName = _lastResult.ServerName,
                durationText = _lastResult.DurationText,
                uploadMessage = _lastResult.UploadMessage,
            },
            error = _lastError,
        }));

        app.MapPost("/api/network/speedtest/start", () =>
        {
            if (_running)
                return Results.BadRequest(new { error = "测速进行中" });

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
                    _lastResult = await _speed.RunAsync(token, p => _lastProgress = p);
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

            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/network/speedtest/cancel", () =>
        {
            _speedCts?.Cancel();
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/network/elevation", () =>
            Results.Json(new { isElevated = ElevationHelper.IsAdministrator() }));

        app.MapPost("/api/network/elevation/restart", async (HttpRequest req) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var asAdmin = !doc.RootElement.TryGetProperty("asAdmin", out var a) || a.GetBoolean();
            HostCallbacks.RestartElevated?.Invoke(asAdmin);
            return Results.Ok(new { ok = true });
        });
    }
}
