namespace DevEnv.Core.ServiceWatch.Models;

/// <summary>服务启动类型（界面可修改的四种）。</summary>
public enum ServiceStartupMode
{
    Automatic,
    AutomaticDelayed,
    Manual,
    Disabled,
}
