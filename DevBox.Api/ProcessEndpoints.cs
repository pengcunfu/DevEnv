using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ProcessWatch.Models;
using ProcessWatch.Services;

namespace DevBox.Api;

public static class ProcessEndpoints
{
    public static void MapProcessEndpoints(this WebApplication app)
    {
        var monitor = new ProcessMonitorService { SortMode = ProcessSortMode.Cpu };
        // Warm first sample so second request has CPU deltas.
        _ = monitor.CollectSnapshot();

        app.MapGet("/api/processes", (string? sort) =>
        {
            monitor.SortMode = sort?.ToLowerInvariant() switch
            {
                "memory" => ProcessSortMode.Memory,
                "name" => ProcessSortMode.Name,
                _ => ProcessSortMode.Cpu,
            };
            var snapshot = monitor.CollectSnapshot();
            return Results.Json(ToDto(snapshot));
        });

        app.MapPost("/api/processes/{pid:int}/kill", (int pid, bool? tree) =>
        {
            if (!ProcessActions.TryKill(pid, tree == true, out var error))
                return Results.BadRequest(new { error });
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/processes/{pid:int}/open-folder", (int pid) =>
        {
            string? path = null;
            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById(pid);
                path = proc.MainModule?.FileName;
            }
            catch
            {
                // ignore
            }

            if (!ProcessActions.TryOpenFolder(path, out var error))
                return Results.BadRequest(new { error });
            return Results.Ok(new { ok = true });
        });
    }

    private static object ToDto(ProcessSnapshot snapshot) => new
    {
        processCount = snapshot.ProcessCount,
        totalWorkingSetBytes = snapshot.TotalWorkingSetBytes,
        totalMemoryText = snapshot.TotalMemoryText,
        timestamp = snapshot.Timestamp,
        statusHint = snapshot.StatusHint,
        processes = snapshot.Processes.Select(p => new
        {
            name = p.Name,
            displayName = p.DisplayName,
            processId = p.ProcessId,
            cpuPercent = p.CpuPercent,
            cpuText = p.CpuText,
            workingSetBytes = p.WorkingSetBytes,
            memoryText = p.MemoryText,
            privateMemoryText = p.PrivateMemoryText,
            virtualMemoryText = p.VirtualMemoryText,
            threadCount = p.ThreadCount,
            handleCount = p.HandleCount,
            path = p.ExecutablePath,
            pathText = p.PathText,
            startTimeText = p.StartTimeText,
            respondingText = p.RespondingText,
        }),
    };
}
