using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ProgramBox.Models;

namespace ProgramBox.Utils
{
    /// <summary>
    /// 开发环境服务管理器：以进程模式控制 MySQL / Redis / PostgreSQL 的启动与停止
    /// </summary>
    public static class DevServicesManager
    {
        // 本地开发环境安装路径（按需修改）
        private const string MySqlHome = @"D:\App\Code\DB\mysql-8.0.44-winx64";
        private const string RedisHome = @"D:\App\Code\DB\redis-8.4.0";
        private const string PostgreSqlHome = @"D:\App\Code\DB\PostgreSQL";

        /// <summary>
        /// 内置服务列表
        /// </summary>
        public static IReadOnlyList<DevService> Services { get; } = new List<DevService>
        {
            new DevService
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
                StopArgs = "-u root shutdown"
            },
            new DevService
            {
                Key = "redis",
                Name = "Redis 8.4",
                Description = "Redis 8.4.0 内存缓存数据库",
                InstallPath = RedisHome,
                DataPath = RedisHome,
                ProcessName = "redis-server",
                StartExe = Path.Combine(RedisHome, "redis-server.exe"),
                // 相对路径：msys2 版 redis 无法解析 Windows 绝对路径参数（与 start.bat 的 cd /d %~dp0 一致），依赖 WorkingDirectory 定位 redis.conf
                StartArgs = "redis.conf",
                StopExe = Path.Combine(RedisHome, "redis-cli.exe"),
                StopArgs = "shutdown"
            },
            new DevService
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
                StopArgs = $"-D \"{Path.Combine(PostgreSqlHome, "data")}\" stop -m fast"
            }
        };

        /// <summary>
        /// 检测服务是否正在运行
        /// </summary>
        public static bool IsRunning(DevService service)
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

        /// <summary>
        /// 启动服务
        /// </summary>
        public static void Start(DevService service)
        {
            if (IsRunning(service))
                return;

            RunProcess(service.StartExe, service.StartArgs, service.InstallPath);
        }

        /// <summary>
        /// 停止服务：优先使用服务自带命令优雅停止，失败则结束进程
        /// </summary>
        public static void Stop(DevService service)
        {
            if (!IsRunning(service))
                return;

            var stopped = false;
            if (!string.IsNullOrEmpty(service.StopExe))
            {
                try
                {
                    RunProcess(service.StopExe, service.StopArgs, service.InstallPath, waitForExit: true, waitMs: 20000);

                    // 等待进程自然退出，最多 4 秒
                    for (var i = 0; i < 20 && IsRunning(service); i++)
                        Thread.Sleep(200);
                    stopped = !IsRunning(service);
                }
                catch
                {
                    // 忽略，走进程结束兜底
                }
            }

            if (!stopped)
                KillProcesses(service.ProcessName);
        }

        /// <summary>
        /// 启动进程并（可选）等待退出
        /// </summary>
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
                WorkingDirectory = workingDirectory
            };

            using var process = Process.Start(psi);
            if (process != null && waitForExit)
                process.WaitForExit(waitMs);
        }

        /// <summary>
        /// 结束指定名称的全部进程（兜底停止方式）
        /// </summary>
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
                    // 忽略单个进程结束失败
                }
            }
        }
    }
}
