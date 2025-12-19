using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using DevEnv.Models;

namespace DevEnv.Services
{
    public class ServiceManager
    {
        private readonly Dictionary<string, string[]> _serviceNames;
        private readonly System.Timers.Timer _timer;
        private readonly Dictionary<string, ServiceStatusInfo> _serviceStatuses;

        public event EventHandler<ServiceStatusUpdatedEventArgs>? ServiceStatusUpdated;

        public ServiceManager()
        {
            _serviceNames = new Dictionary<string, string[]>
            {
                { "MySQL", new[] { "MySQL" } },
                { "Redis", new[] { "Redis" } },
                { "MongoDB", new[] { "MongoDB" } },
                { "MinIO", new[] { "MinIO" } }
            };

            _serviceStatuses = new Dictionary<string, ServiceStatusInfo>();
            _timer = new System.Timers.Timer(2000); // Check every 2 seconds
            _timer.Elapsed += CheckAllServicesStatus;
        }

        public void StartMonitoring()
        {
            _timer.Start();
        }

        public void StopMonitoring()
        {
            _timer.Stop();
        }

        private void CheckAllServicesStatus(object? sender, ElapsedEventArgs e)
        {
            foreach (var serviceName in _serviceNames.Keys)
            {
                var status = GetServiceStatus(serviceName);
                _serviceStatuses[serviceName] = status;
                ServiceStatusUpdated?.Invoke(this, new ServiceStatusUpdatedEventArgs(serviceName, status));
            }
        }

        public ServiceStatusInfo GetServiceStatus(string serviceName)
        {
            if (!_serviceNames.ContainsKey(serviceName))
                return new ServiceStatusInfo { Status = ServiceState.NotFound, DisplayText = "未知服务", Color = "Gray" };

            var possibleNames = _serviceNames[serviceName];

            foreach (var name in possibleNames)
            {
                try
                {
                    using var sc = new ServiceController(name);
                    switch (sc.Status)
                    {
                        case ServiceControllerStatus.Running:
                            return new ServiceStatusInfo { Status = ServiceState.Running, DisplayText = "运行中", Color = "Green" };
                        case ServiceControllerStatus.Stopped:
                            return new ServiceStatusInfo { Status = ServiceState.Stopped, DisplayText = "已停止", Color = "Red" };
                        case ServiceControllerStatus.Paused:
                            return new ServiceStatusInfo { Status = ServiceState.Paused, DisplayText = "已暂停", Color = "Orange" };
                        case ServiceControllerStatus.StartPending:
                            return new ServiceStatusInfo { Status = ServiceState.Starting, DisplayText = "启动中", Color = "Blue" };
                        case ServiceControllerStatus.StopPending:
                            return new ServiceStatusInfo { Status = ServiceState.Stopping, DisplayText = "停止中", Color = "Orange" };
                        default:
                            return new ServiceStatusInfo { Status = ServiceState.Unknown, DisplayText = "未知", Color = "Gray" };
                    }
                }
                catch (InvalidOperationException)
                {
                    // Service not found, try next name
                    continue;
                }
            }

            return new ServiceStatusInfo { Status = ServiceState.NotFound, DisplayText = "未安装", Color = "Red" };
        }

        public async Task<(bool Success, string Message)> StartServiceAsync(string serviceName)
        {
            if (!_serviceNames.ContainsKey(serviceName))
                return (false, $"{serviceName} 服务未找到");

            var possibleNames = _serviceNames[serviceName];

            foreach (var name in possibleNames)
            {
                try
                {
                    using var sc = new ServiceController(name);

                    if (sc.Status == ServiceControllerStatus.Running)
                        return (true, $"{serviceName} 已在运行中");

                    // Use sc.exe for more reliable service control
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "sc.exe",
                            Arguments = $"start \"{name}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.GetEncoding(936), // GBK for Chinese
                            StandardErrorEncoding = Encoding.GetEncoding(936)
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                        return (true, $"{serviceName} 启动成功");
                    else if (process.ExitCode == 1060)
                        continue; // Service not found, try next name
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        if (error.Contains("1058"))
                            return (false, $"{serviceName} 服务已被禁用");
                        else if (error.Contains("1053"))
                            return (false, $"{serviceName} 服务无响应");
                        else
                            return (false, $"启动失败: {error.Trim()}");
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"系统错误: {ex.Message}");
                }
            }

            return (false, $"{serviceName} 服务未找到");
        }

        public async Task<(bool Success, string Message)> StopServiceAsync(string serviceName)
        {
            if (!_serviceNames.ContainsKey(serviceName))
                return (false, $"{serviceName} 服务未找到");

            var possibleNames = _serviceNames[serviceName];

            foreach (var name in possibleNames)
            {
                try
                {
                    using var sc = new ServiceController(name);

                    if (sc.Status == ServiceControllerStatus.Stopped)
                        return (true, $"{serviceName} 已停止");

                    // Use sc.exe for more reliable service control
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "sc.exe",
                            Arguments = $"stop \"{name}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.GetEncoding(936),
                            StandardErrorEncoding = Encoding.GetEncoding(936)
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                        return (true, $"{serviceName} 停止成功");
                    else if (process.ExitCode == 1060)
                        continue; // Service not found, try next name
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        if (error.Contains("1062"))
                            return (true, $"{serviceName} 已停止");
                        else
                            return (false, $"停止失败: {error.Trim()}");
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"系统错误: {ex.Message}");
                }
            }

            return (false, $"{serviceName} 服务未找到");
        }
    }
}