using System.ServiceProcess;

namespace DevEnv.Core.ServiceWatch.Models;

/// <summary>单条 Windows 服务快照，供列表与详情绑定。</summary>
public sealed class ServiceInfo
{
    public required string ServiceName { get; init; }
    public required string DisplayName { get; init; }
    public string Description { get; init; } = string.Empty;

    public ServiceControllerStatus? Status { get; init; }
    public ServiceStartMode? StartType { get; init; }
    public bool IsDelayedAutoStart { get; init; }
    public ServiceType ServiceType { get; init; }

    public bool CanStop { get; init; }
    public bool CanPauseAndContinue { get; init; }

    /// <summary>注册表里的登录身份（ObjectName）。</summary>
    public string LogOnAs { get; init; } = string.Empty;

    /// <summary>注册表里的原始 ImagePath（可能带引号、参数与环境变量）。</summary>
    public string ImagePath { get; init; } = string.Empty;

    /// <summary>从 ImagePath 解析出的可执行文件路径。</summary>
    public string ExecutablePath { get; init; } = string.Empty;

    public IReadOnlyList<string> ServicesDependedOn { get; init; } = [];
    public IReadOnlyList<string> DependentServices { get; init; } = [];

    /// <summary>本服务依赖的其他服务（显示名，顿号分隔）。</summary>
    public string DependenciesText { get; init; } = string.Empty;

    /// <summary>依赖本服务的其他服务（显示名，顿号分隔）。</summary>
    public string DependentsText { get; init; } = string.Empty;

    public bool IsRunning => Status == ServiceControllerStatus.Running;
    public bool IsStopped => Status == ServiceControllerStatus.Stopped;
    public bool IsPaused => Status == ServiceControllerStatus.Paused;
    public bool IsRunningOrPaused => IsRunning || IsPaused;

    public string StatusText => ServiceText.DescribeStatus(Status);
    public string StartTypeText => ServiceText.DescribeStartType(StartType, IsDelayedAutoStart);
    public string ServiceTypeText => ServiceText.DescribeServiceType(ServiceType);
    public string LogOnAsText => string.IsNullOrWhiteSpace(LogOnAs) ? "—" : LogOnAs;
    public string DescriptionText => string.IsNullOrWhiteSpace(Description) ? "—" : Description;
    public string ImagePathText => string.IsNullOrWhiteSpace(ImagePath) ? "—" : ImagePath;
    public string DependenciesDisplayText => string.IsNullOrWhiteSpace(DependenciesText) ? "无" : DependenciesText;
    public string DependentsDisplayText => string.IsNullOrWhiteSpace(DependentsText) ? "无" : DependentsText;

    public override string ToString() => DisplayName;

    /// <summary>状态排序权重：运行中的服务排在前面。</summary>
    public int StatusRank => Status switch
    {
        ServiceControllerStatus.Running => 0,
        ServiceControllerStatus.StartPending => 1,
        ServiceControllerStatus.ContinuePending => 2,
        ServiceControllerStatus.PausePending => 3,
        ServiceControllerStatus.Paused => 4,
        ServiceControllerStatus.StopPending => 5,
        ServiceControllerStatus.Stopped => 6,
        _ => 7,
    };
}
