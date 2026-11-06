using System.ServiceProcess;

namespace DevEnv.Core.ServiceWatch.Models;

/// <summary>服务状态的显示文案与注册表值映射（纯函数，便于单元测试）。</summary>
public static class ServiceText
{
    public static string DescribeStatus(ServiceControllerStatus? status) => status switch
    {
        ServiceControllerStatus.Running => "运行中",
        ServiceControllerStatus.StartPending => "启动中",
        ServiceControllerStatus.StopPending => "停止中",
        ServiceControllerStatus.Paused => "已暂停",
        ServiceControllerStatus.PausePending => "暂停中",
        ServiceControllerStatus.ContinuePending => "继续中",
        ServiceControllerStatus.Stopped => "已停止",
        _ => "未知",
    };

    public static string DescribeStartType(ServiceStartMode? startType, bool delayedAutoStart) => startType switch
    {
        ServiceStartMode.Automatic => delayedAutoStart ? "自动（延迟启动）" : "自动",
        ServiceStartMode.Manual => "手动",
        ServiceStartMode.Disabled => "禁用",
        ServiceStartMode.Boot => "引导",
        ServiceStartMode.System => "系统",
        _ => "未知",
    };

    public static string DescribeStartupMode(ServiceStartupMode mode) => mode switch
    {
        ServiceStartupMode.Automatic => "自动",
        ServiceStartupMode.AutomaticDelayed => "自动（延迟启动）",
        ServiceStartupMode.Manual => "手动",
        ServiceStartupMode.Disabled => "禁用",
        _ => "未知",
    };

    public static string DescribeServiceType(ServiceType serviceType)
    {
        if (serviceType.HasFlag(ServiceType.InteractiveProcess))
            return "交互式进程";
        if (serviceType.HasFlag(ServiceType.Win32OwnProcess))
            return "Win32 独立进程";
        if (serviceType.HasFlag(ServiceType.Win32ShareProcess))
            return "Win32 共享进程";
        if (serviceType.HasFlag(ServiceType.Adapter))
            return "适配器";
        if (serviceType.HasFlag(ServiceType.FileSystemDriver))
            return "文件系统驱动";
        if (serviceType.HasFlag(ServiceType.RecognizerDriver))
            return "识别器驱动";
        if (serviceType.HasFlag(ServiceType.KernelDriver))
            return "内核驱动";
        return "未知";
    }

    /// <summary>把注册表 <c>Start</c> 值映射为启动类型。</summary>
    public static ServiceStartMode? StartModeFromRegistry(int? startCode) => startCode switch
    {
        0 => ServiceStartMode.Boot,
        1 => ServiceStartMode.System,
        2 => ServiceStartMode.Automatic,
        3 => ServiceStartMode.Manual,
        4 => ServiceStartMode.Disabled,
        _ => null,
    };

    public static ServiceStartupMode ToStartupMode(ServiceStartMode? startType, bool delayedAutoStart) => startType switch
    {
        ServiceStartMode.Automatic when delayedAutoStart => ServiceStartupMode.AutomaticDelayed,
        ServiceStartMode.Automatic => ServiceStartupMode.Automatic,
        ServiceStartMode.Manual => ServiceStartupMode.Manual,
        ServiceStartMode.Disabled => ServiceStartupMode.Disabled,
        _ => ServiceStartupMode.Manual,
    };

    /// <summary>把依赖服务的内部名映射成显示名，按原顺序去重后用顿号连接。</summary>
    public static string DescribeDependencies(
        IEnumerable<string> serviceNames,
        IReadOnlyDictionary<string, string> displayNames)
    {
        var labels = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in serviceNames)
        {
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                continue;

            labels.Add(displayNames.TryGetValue(name, out var display) && !string.IsNullOrWhiteSpace(display)
                ? display
                : name);
        }

        return string.Join("、", labels);
    }
}
