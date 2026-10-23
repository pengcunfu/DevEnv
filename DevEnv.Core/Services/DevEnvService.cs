using System.Diagnostics;
using DevEnv.Core.Models;

namespace DevEnv.Core.Services;

/// <summary>开发环境服务（MySQL / Redis / PostgreSQL 进程启停），逻辑来自 <c>DevEnvEndpoints</c>。</summary>
public sealed class DevEnvService
{
    public IReadOnlyList<DevServiceItem> GetServices() =>
        DevServicesManager.Services.Select(s =>
        {
            var running = DevServicesManager.IsRunning(s);
            var installed = DevServicesManager.IsInstalled(s);
            return new DevServiceItem
            {
                Key = s.Key,
                Name = s.Name,
                Description = s.Description,
                InstallPath = s.InstallPath,
                DataPath = s.DataPath,
                Running = running,
                Installed = installed,
                StatusText = !installed ? "未安装" : running ? "运行中" : "已停止",
                CanStart = installed && !running,
                CanStop = installed && running,
            };
        }).ToList();

    public OperationResult Start(string key)
    {
        var svc = DevServicesManager.Find(key);
        if (svc is null)
            return OperationResult.Fail("未知服务");
        try
        {
            DevServicesManager.Start(svc);
            return OperationResult.Success($"{svc.Name} 已启动");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult Stop(string key)
    {
        var svc = DevServicesManager.Find(key);
        if (svc is null)
            return OperationResult.Fail("未知服务");
        try
        {
            DevServicesManager.Stop(svc);
            return OperationResult.Success($"{svc.Name} 已停止");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult OpenDataDirectory(string key)
    {
        var svc = DevServicesManager.Find(key);
        if (svc is null)
            return OperationResult.Fail("未知服务");

        var dir = Directory.Exists(svc.DataPath) ? svc.DataPath : svc.InstallPath;
        if (!Directory.Exists(dir))
            return OperationResult.Fail("目录不存在");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{dir}\"",
                UseShellExecute = true,
            });
            return OperationResult.Success(dir);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}