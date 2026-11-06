using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using DevEnv.Core.LavaEnv.Services;
using DevEnv.Core.Models;
using DevEnv.Core.ServiceWatch.Models;
using Microsoft.Win32;

namespace DevEnv.Core.ServiceWatch.Services;

/// <summary>
/// Windows 服务管理：枚举服务、启停 / 重启 / 暂停、修改启动类型与读取依赖关系。
/// 只使用 SCM（<see cref="ServiceController"/>）与注册表，不依赖 WMI。
/// </summary>
public sealed class ServiceManagerService
{
    private const string ServicesRegistryPath = @"SYSTEM\CurrentControlSet\Services\";
    private const string AccessDeniedMessage = "需要管理员权限：请点「管理员重启」后重试。";

    public bool IsElevated
    {
        get
        {
            try
            {
                return ElevationHelper.IsAdministrator();
            }
            catch
            {
                return false;
            }
        }
    }

    public ServiceSnapshot GetSnapshot()
    {
        var now = DateTime.Now;
        var elevated = IsElevated;

        ServiceController[] controllers;
        try
        {
            controllers = ServiceController.GetServices();
        }
        catch (Exception ex)
        {
            return new ServiceSnapshot
            {
                Services = [],
                Timestamp = now,
                IsElevated = elevated,
                StatusHint = $"无法枚举系统服务：{DescribeException(ex)}",
            };
        }

        var drafts = new List<ServiceDraft>(controllers.Length);
        var skipped = 0;

        foreach (var controller in controllers)
        {
            try
            {
                var name = Safe(() => controller.ServiceName, string.Empty);
                if (string.IsNullOrWhiteSpace(name))
                {
                    skipped++;
                    continue;
                }

                var displayName = Safe(() => controller.DisplayName, name);
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = name;

                var registry = ReadRegistry(name);
                var status = SafeNullable<ServiceControllerStatus>(() => controller.Status);
                var startType = SafeNullable<ServiceStartMode>(() => controller.StartType);
                startType ??= ServiceText.StartModeFromRegistry(registry.StartCode);

                drafts.Add(new ServiceDraft
                {
                    Name = name,
                    DisplayName = displayName,
                    Description = registry.Description,
                    Status = status,
                    StartType = startType,
                    DelayedAutoStart = registry.DelayedAutoStart,
                    ServiceType = Safe(() => controller.ServiceType, (ServiceType)0),
                    CanStop = Safe(() => controller.CanStop, false),
                    CanPauseAndContinue = Safe(() => controller.CanPauseAndContinue, false),
                    LogOnAs = registry.LogOnAs,
                    ImagePath = registry.ImagePath,
                    DependsOn = registry.DependsOn,
                });
            }
            catch
            {
                skipped++;
            }
            finally
            {
                try { controller.Dispose(); } catch { /* ignore */ }
            }
        }

        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var draft in drafts)
            displayNames[draft.Name] = draft.DisplayName;

        var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var draft in drafts)
        {
            foreach (var dependency in draft.DependsOn)
            {
                if (string.IsNullOrWhiteSpace(dependency)
                    || string.Equals(dependency, draft.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!dependents.TryGetValue(dependency, out var list))
                    dependents[dependency] = list = [];

                list.Add(draft.Name);
            }
        }

        var services = drafts
            .Select(draft =>
            {
                var dependsOn = dependents.TryGetValue(draft.Name, out var list) ? list : [];
                return new ServiceInfo
                {
                    ServiceName = draft.Name,
                    DisplayName = draft.DisplayName,
                    Description = draft.Description,
                    Status = draft.Status,
                    StartType = draft.StartType,
                    IsDelayedAutoStart = draft.DelayedAutoStart,
                    ServiceType = draft.ServiceType,
                    CanStop = draft.CanStop,
                    CanPauseAndContinue = draft.CanPauseAndContinue,
                    LogOnAs = draft.LogOnAs,
                    ImagePath = draft.ImagePath,
                    ExecutablePath = ServicePath.ExtractExecutable(draft.ImagePath),
                    ServicesDependedOn = draft.DependsOn,
                    DependentServices = dependsOn,
                    DependenciesText = ServiceText.DescribeDependencies(draft.DependsOn, displayNames),
                    DependentsText = ServiceText.DescribeDependencies(dependsOn, displayNames),
                };
            })
            .OrderBy(service => service.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(service => service.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hints = new List<string>();
        if (!elevated)
            hints.Add("普通权限：启动、停止与修改启动类型需要管理员");
        if (skipped > 0)
            hints.Add($"{skipped} 个服务无法读取");

        return new ServiceSnapshot
        {
            Services = services,
            Timestamp = now,
            TotalCount = services.Count,
            RunningCount = services.Count(service => service.IsRunning),
            StoppedCount = services.Count(service => service.IsStopped),
            IsElevated = elevated,
            StatusHint = hints.Count == 0 ? null : string.Join(" · ", hints),
        };
    }

    public OperationResult Start(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            if (controller.Status == ServiceControllerStatus.Running)
                return OperationResult.Success("服务已在运行");

            controller.Start();
            return WaitFor(controller, ServiceControllerStatus.Running, TimeSpan.FromSeconds(12),
                "服务已启动", "启动请求已发送，服务仍在启动中");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(DescribeException(ex));
        }
    }

    public OperationResult Stop(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            if (controller.Status == ServiceControllerStatus.Stopped)
                return OperationResult.Success("服务已停止");

            var blocker = FindRunningDependents(controller);
            if (blocker is not null)
                return OperationResult.Fail($"请先停止依赖它的服务：{blocker}");

            if (!controller.CanStop)
                return OperationResult.Fail("该服务不允许停止");

            controller.Stop();
            return WaitFor(controller, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15),
                "服务已停止", "停止请求已发送，服务仍在停止中");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(DescribeException(ex));
        }
    }

    public OperationResult Restart(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            if (controller.Status == ServiceControllerStatus.Stopped)
                return OperationResult.Fail("服务当前未运行，无需重启");

            var blocker = FindRunningDependents(controller);
            if (blocker is not null)
                return OperationResult.Fail($"请先停止依赖它的服务：{blocker}");

            if (!controller.CanStop)
                return OperationResult.Fail("该服务不允许停止");

            controller.Stop();
            if (!TryWaitFor(controller, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15)))
                return OperationResult.Fail("服务停止超时，重启未完成；请稍后重试。");

            controller.Start();
            return WaitFor(controller, ServiceControllerStatus.Running, TimeSpan.FromSeconds(15),
                "服务已重启", "启动请求已发送，服务仍在启动中");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(DescribeException(ex));
        }
    }

    public OperationResult Pause(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            if (!controller.CanPauseAndContinue)
                return OperationResult.Fail("该服务不支持暂停");
            if (controller.Status == ServiceControllerStatus.Paused)
                return OperationResult.Success("服务已暂停");

            controller.Pause();
            return WaitFor(controller, ServiceControllerStatus.Paused, TimeSpan.FromSeconds(12),
                "服务已暂停", "暂停请求已发送，服务仍在处理中");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(DescribeException(ex));
        }
    }

    public OperationResult Continue(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            if (!controller.CanPauseAndContinue)
                return OperationResult.Fail("该服务不支持继续");
            if (controller.Status == ServiceControllerStatus.Running)
                return OperationResult.Success("服务已在运行");

            controller.Continue();
            return WaitFor(controller, ServiceControllerStatus.Running, TimeSpan.FromSeconds(12),
                "服务已恢复运行", "继续请求已发送，服务仍在处理中");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(DescribeException(ex));
        }
    }

    /// <summary>修改启动类型（自动 / 自动延迟 / 手动 / 禁用）。</summary>
    public OperationResult SetStartupType(string serviceName, ServiceStartupMode mode)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return OperationResult.Fail("服务名为空");
        if (!IsElevated)
            return OperationResult.Fail(AccessDeniedMessage);

        var (nativeStartType, delayed) = mode switch
        {
            ServiceStartupMode.Automatic => (ServiceStartAuto, false),
            ServiceStartupMode.AutomaticDelayed => (ServiceStartAuto, true),
            ServiceStartupMode.Disabled => (ServiceStartDisabled, false),
            _ => (ServiceStartDemand, false),
        };

        var scm = OpenSCManager(null, null, ScManagerConnect);
        if (scm == IntPtr.Zero)
            return OperationResult.Fail(FormatWin32Error());

        try
        {
            var service = OpenService(scm, serviceName, ServiceChangeConfig);
            if (service == IntPtr.Zero)
                return OperationResult.Fail(FormatWin32Error());

            try
            {
                if (!ChangeServiceConfig(service, ServiceNoChange, nativeStartType, ServiceNoChange,
                        null, null, IntPtr.Zero, IntPtr.Zero, null, null, null))
                    return OperationResult.Fail(FormatWin32Error());

                var delayedInfo = new ServiceDelayedAutoStartInfo
                {
                    DelayedAutostart = delayed ? 1 : 0,
                };
                if (!ChangeServiceConfig2(service, ServiceConfigDelayedAutoStartInfo, ref delayedInfo)
                    && nativeStartType == ServiceStartAuto)
                    return OperationResult.Fail("启动类型已改为自动，但延迟启动设置失败：" + FormatWin32Error());

                return OperationResult.Success(
                    $"「{serviceName}」的启动类型已改为「{ServiceText.DescribeStartupMode(mode)}」");
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    public OperationResult RestartElevated(bool asAdmin)
    {
        if (HostCallbacks.RestartElevated is null)
            return OperationResult.Fail("宿主未接管重启回调");

        HostCallbacks.RestartElevated(asAdmin);
        return OperationResult.Success(asAdmin ? "正在以管理员身份重启" : "正在降权重启");
    }

    private static string? FindRunningDependents(ServiceController controller)
    {
        var running = new List<string>();
        try
        {
            foreach (var dependent in controller.DependentServices)
            {
                try
                {
                    if (dependent.Status != ServiceControllerStatus.Stopped)
                        running.Add(Safe(() => dependent.DisplayName, dependent.ServiceName));
                }
                catch
                {
                    running.Add(dependent.ServiceName);
                }
                finally
                {
                    try { dependent.Dispose(); } catch { /* ignore */ }
                }
            }
        }
        catch
        {
            // 拿不到依赖关系时交给 SCM 自己报错
            return null;
        }

        return running.Count == 0 ? null : string.Join("、", running);
    }

    private static OperationResult WaitFor(
        ServiceController controller,
        ServiceControllerStatus target,
        TimeSpan timeout,
        string successMessage,
        string timeoutMessage)
    {
        try
        {
            controller.WaitForStatus(target, timeout);
            return OperationResult.Success(successMessage);
        }
        catch (System.TimeoutException)
        {
            return OperationResult.Success(timeoutMessage);
        }
    }

    private static bool TryWaitFor(ServiceController controller, ServiceControllerStatus target, TimeSpan timeout)
    {
        try
        {
            controller.WaitForStatus(target, timeout);
            return true;
        }
        catch (System.TimeoutException)
        {
            return false;
        }
    }

    private static ServiceRegistryData ReadRegistry(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ServicesRegistryPath + serviceName);
            if (key is null)
                return new ServiceRegistryData();

            return new ServiceRegistryData
            {
                Description = ResolveDescription(key.GetValue("Description") as string),
                LogOnAs = (key.GetValue("ObjectName") as string ?? string.Empty).Trim(),
                ImagePath = (key.GetValue("ImagePath") as string ?? string.Empty).Trim(),
                DelayedAutoStart = key.GetValue("DelayedAutostart") is int delayed && delayed == 1,
                DependsOn = key.GetValue("DependOnService") as string[] ?? [],
                StartCode = key.GetValue("Start") is int start ? start : null,
            };
        }
        catch
        {
            return new ServiceRegistryData();
        }
    }

    /// <summary>注册表里的描述可能是 @dll,-id 形式的本地化引用，交给系统解析。</summary>
    private static string ResolveDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        raw = raw.Trim();
        if (!raw.StartsWith('@'))
            return raw;

        try
        {
            var buffer = new StringBuilder(1024);
            if (SHLoadIndirectString(raw, buffer, buffer.Capacity, IntPtr.Zero) == 0)
            {
                var text = buffer.ToString().Trim();
                if (text.Length > 0)
                    return text;
            }
        }
        catch
        {
            // 解析失败就显示原始引用
        }

        return raw;
    }

    private static string DescribeException(Exception ex)
    {
        var code = FindNativeErrorCode(ex);
        return code switch
        {
            5 => AccessDeniedMessage,
            1053 => "服务没有及时响应启动或控制请求。",
            1056 => "服务已在运行。",
            1058 => "服务已被禁用，请先把启动类型改为手动或自动。",
            1060 => "服务不存在或已被删除。",
            1062 => "服务未在运行。",
            1067 => "服务进程意外终止。",
            1072 => "服务已被标记为删除，请重启系统后再试。",
            1223 => "操作已取消。",
            _ => ex.Message,
        };
    }

    private static int? FindNativeErrorCode(Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is Win32Exception win32)
                return win32.NativeErrorCode;
            if (ex is UnauthorizedAccessException)
                return 5;
            ex = ex.InnerException;
        }

        return null;
    }

    private static string FormatWin32Error()
    {
        var code = Marshal.GetLastWin32Error();
        return code == 5
            ? AccessDeniedMessage
            : $"操作失败（错误 {code}）：{new Win32Exception(code).Message}";
    }

    private static T Safe<T>(Func<T> getter, T fallback)
    {
        try { return getter(); }
        catch { return fallback; }
    }

    private static T? SafeNullable<T>(Func<T> getter) where T : struct
    {
        try { return getter(); }
        catch { return null; }
    }

    private sealed class ServiceDraft
    {
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public string Description { get; init; } = string.Empty;
        public ServiceControllerStatus? Status { get; init; }
        public ServiceStartMode? StartType { get; init; }
        public bool DelayedAutoStart { get; init; }
        public ServiceType ServiceType { get; init; }
        public bool CanStop { get; init; }
        public bool CanPauseAndContinue { get; init; }
        public string LogOnAs { get; init; } = string.Empty;
        public string ImagePath { get; init; } = string.Empty;
        public IReadOnlyList<string> DependsOn { get; init; } = [];
    }

    private sealed class ServiceRegistryData
    {
        public string Description { get; init; } = string.Empty;
        public string LogOnAs { get; init; } = string.Empty;
        public string ImagePath { get; init; } = string.Empty;
        public bool DelayedAutoStart { get; init; }
        public IReadOnlyList<string> DependsOn { get; init; } = [];
        public int? StartCode { get; init; }
    }

    #region Native

    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceChangeConfig = 0x0002;
    private const uint ServiceNoChange = 0xFFFFFFFF;

    private const uint ServiceStartAuto = 2;
    private const uint ServiceStartDemand = 3;
    private const uint ServiceStartDisabled = 4;

    private const uint ServiceConfigDelayedAutoStartInfo = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceDelayedAutoStartInfo
    {
        public int DelayedAutostart;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr scManager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig(
        IntPtr service,
        uint serviceType,
        uint startType,
        uint errorControl,
        string? binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        IntPtr dependencies,
        string? serviceStartName,
        string? password,
        string? displayName);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig2(
        IntPtr service,
        uint infoLevel,
        ref ServiceDelayedAutoStartInfo info);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHLoadIndirectString(
        string source,
        StringBuilder buffer,
        int bufferSize,
        IntPtr reserved);

    #endregion
}
