using System.Diagnostics;

namespace DevEnv.Core;

public sealed class DevServiceInfo
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string InstallPath { get; init; }
    public required string DataPath { get; init; }
    public required string ProcessName { get; init; }
    public required string StartExe { get; init; }
    public required string StartArgs { get; init; }
    public required string StopExe { get; init; }
    public required string StopArgs { get; init; }
}

/// <summary>开发环境服务：进程模式控制 MySQL / Redis / PostgreSQL。</summary>
public static class DevServicesManager
{
    private const string MySqlHome = @"D:\App\Code\DB\mysql-8.0.44-winx64";
    private const string RedisHome = @"D:\App\Code\DB\redis-8.4.0";
    private const string PostgreSqlHome = @"D:\App\Code\DB\PostgreSQL";

    public static IReadOnlyList<DevServiceInfo> Services { get; } =
    [
        new DevServiceInfo
        {
            Key = "mysql",
            Name = "MySQL 8.0",
            Description = "MySQL 8.0.44 关系型数据库",
            InstallPath = MySqlHome,
            DataPath = Path.Combine(MySqlHome, "data"),
            ProcessName = "mysqld",
            StartExe = Path.Combine(MySqlHome, "bin", "mysqld.exe"),
            StartArgs = $"--defaults-file=\"{Path.Combine(MySqlHome, "my.ini")}\"",
            StopExe = Path.Combine(MySqlHome, "bin", "mysqladmin.exe"),
            StopArgs = "-u root shutdown",
        },
        new DevServiceInfo
        {
            Key = "redis",
            Name = "Redis 8.4",
            Description = "Redis 8.4.0 内存缓存数据库",
            InstallPath = RedisHome,
            DataPath = RedisHome,
            ProcessName = "redis-server",
            StartExe = Path.Combine(RedisHome, "redis-server.exe"),
            StartArgs = "redis.conf",
            StopExe = Path.Combine(RedisHome, "redis-cli.exe"),
            StopArgs = "shutdown",
        },
        new DevServiceInfo
        {
            Key = "postgresql",
            Name = "PostgreSQL",
            Description = "PostgreSQL 关系型数据库",
            InstallPath = PostgreSqlHome,
            DataPath = Path.Combine(PostgreSqlHome, "data"),
            ProcessName = "postgres",
            StartExe = Path.Combine(PostgreSqlHome, "bin", "pg_ctl.exe"),
            StartArgs = $"-D \"{Path.Combine(PostgreSqlHome, "data")}\" start",
            StopExe = Path.Combine(PostgreSqlHome, "bin", "pg_ctl.exe"),
            StopArgs = $"-D \"{Path.Combine(PostgreSqlHome, "data")}\" stop -m fast",
        },
    ];

    public static DevServiceInfo? Find(string key) =>
        Services.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsRunning(DevServiceInfo service)
    {
        try
        {
            return Process.GetProcessesByName(service.ProcessName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsInstalled(DevServiceInfo service) => File.Exists(service.StartExe);

    public static void Start(DevServiceInfo service)
    {
        if (IsRunning(service))
            return;
        RunProcess(service.StartExe, service.StartArgs, service.InstallPath);
    }

    public static void Stop(DevServiceInfo service)
    {
        if (!IsRunning(service))
            return;

        var stopped = false;
        if (!string.IsNullOrEmpty(service.StopExe))
        {
            try
            {
                RunProcess(service.StopExe, service.StopArgs, service.InstallPath, waitForExit: true, waitMs: 20000);
                for (var i = 0; i < 20 && IsRunning(service); i++)
                    Thread.Sleep(200);
                stopped = !IsRunning(service);
            }
            catch
            {
                // fall through
            }
        }

        if (!stopped)
            KillProcesses(service.ProcessName);
    }

    private static void RunProcess(string exe, string args, string workingDirectory, bool waitForExit = false, int waitMs = 0)
    {
        if (!File.Exists(exe))
            throw new FileNotFoundException($"找不到程序: {exe}");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };

        using var process = Process.Start(psi);
        if (process is not null && waitForExit)
            process.WaitForExit(waitMs);
    }

    private static void KillProcesses(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                process.Kill();
                process.WaitForExit(5000);
            }
            catch
            {
                // ignore
            }
        }
    }
}
