using System.ServiceProcess;
using DevEnv.Core.ServiceWatch.Models;

namespace DevEnv.Tests;

public class ServiceTextTests
{
    [Theory]
    [InlineData(ServiceControllerStatus.Running, "运行中")]
    [InlineData(ServiceControllerStatus.Stopped, "已停止")]
    [InlineData(ServiceControllerStatus.StartPending, "启动中")]
    [InlineData(ServiceControllerStatus.StopPending, "停止中")]
    [InlineData(ServiceControllerStatus.Paused, "已暂停")]
    [InlineData(ServiceControllerStatus.PausePending, "暂停中")]
    [InlineData(ServiceControllerStatus.ContinuePending, "继续中")]
    public void DescribeStatus_映射为中文状态(ServiceControllerStatus status, string expected) =>
        Assert.Equal(expected, ServiceText.DescribeStatus(status));

    [Fact]
    public void DescribeStatus_空值显示未知() =>
        Assert.Equal("未知", ServiceText.DescribeStatus(null));

    [Fact]
    public void DescribeStartType_区分延迟启动()
    {
        Assert.Equal("自动", ServiceText.DescribeStartType(ServiceStartMode.Automatic, false));
        Assert.Equal("自动（延迟启动）", ServiceText.DescribeStartType(ServiceStartMode.Automatic, true));
        Assert.Equal("手动", ServiceText.DescribeStartType(ServiceStartMode.Manual, true));
        Assert.Equal("禁用", ServiceText.DescribeStartType(ServiceStartMode.Disabled, false));
        Assert.Equal("引导", ServiceText.DescribeStartType(ServiceStartMode.Boot, false));
        Assert.Equal("系统", ServiceText.DescribeStartType(ServiceStartMode.System, false));
        Assert.Equal("未知", ServiceText.DescribeStartType(null, false));
    }

    [Theory]
    [InlineData(0, ServiceStartMode.Boot)]
    [InlineData(1, ServiceStartMode.System)]
    [InlineData(2, ServiceStartMode.Automatic)]
    [InlineData(3, ServiceStartMode.Manual)]
    [InlineData(4, ServiceStartMode.Disabled)]
    public void StartModeFromRegistry_映射注册表值(int startCode, ServiceStartMode expected)
    {
        Assert.Equal(expected, ServiceText.StartModeFromRegistry(startCode));
        Assert.Null(ServiceText.StartModeFromRegistry(9));
        Assert.Null(ServiceText.StartModeFromRegistry(null));
    }

    [Fact]
    public void ToStartupMode_映射为界面选项()
    {
        Assert.Equal(ServiceStartupMode.AutomaticDelayed,
            ServiceText.ToStartupMode(ServiceStartMode.Automatic, delayedAutoStart: true));
        Assert.Equal(ServiceStartupMode.Automatic,
            ServiceText.ToStartupMode(ServiceStartMode.Automatic, delayedAutoStart: false));
        Assert.Equal(ServiceStartupMode.Manual,
            ServiceText.ToStartupMode(ServiceStartMode.Manual, delayedAutoStart: false));
        Assert.Equal(ServiceStartupMode.Disabled,
            ServiceText.ToStartupMode(ServiceStartMode.Disabled, delayedAutoStart: false));
        Assert.Equal(ServiceStartupMode.Manual,
            ServiceText.ToStartupMode(null, delayedAutoStart: false));
    }

    [Fact]
    public void DescribeDependencies_映射显示名并去重()
    {
        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RpcSs"] = "Remote Procedure Call (RPC)",
            ["StateRepository"] = "State Repository Service",
        };

        var text = ServiceText.DescribeDependencies(
            ["rpcss", "StateRepository", "UnknownService", "RpcSs", ""],
            displayNames);

        Assert.Equal("Remote Procedure Call (RPC)、State Repository Service、UnknownService", text);
        Assert.Equal("", ServiceText.DescribeDependencies([], displayNames));
    }

    [Fact]
    public void DescribeServiceType_映射服务类型()
    {
        Assert.Equal("Win32 独立进程", ServiceText.DescribeServiceType(ServiceType.Win32OwnProcess));
        Assert.Equal("Win32 共享进程", ServiceText.DescribeServiceType(ServiceType.Win32ShareProcess));
        Assert.Equal("交互式进程", ServiceText.DescribeServiceType(ServiceType.InteractiveProcess));
        Assert.Equal("内核驱动", ServiceText.DescribeServiceType(ServiceType.KernelDriver));
        Assert.Equal("未知", ServiceText.DescribeServiceType((ServiceType)0));
    }
}
