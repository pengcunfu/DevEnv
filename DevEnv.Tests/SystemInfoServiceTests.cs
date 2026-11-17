using DevEnv.Core.SystemInfo.Services;

namespace DevEnv.Tests;

public sealed class SystemInfoServiceTests
{
    [Theory]
    [InlineData("Windows 10 Pro", 22631, "Windows 11 Pro")]
    [InlineData("Windows 10 专业版", 26200, "Windows 11 专业版")]
    [InlineData("Windows 10 Pro", 19045, "Windows 10 Pro")]
    [InlineData("Windows Server 2022", 20348, "Windows Server 2022")]
    [InlineData(null, 22631, "Windows")]
    public void NormalizeOsName_CorrectsWindows11ProductName(string? productName, int build, string expected)
    {
        Assert.Equal(expected, SystemInfoService.NormalizeOsName(productName, build));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, "0 分 0 秒")]
    [InlineData(0, 0, 5, 12, "5 分 12 秒")]
    [InlineData(0, 3, 5, 12, "3 小时 5 分")]
    [InlineData(2, 3, 5, 12, "2 天 3 小时 5 分")]
    public void FormatUptime_UsesReadableUnits(int days, int hours, int minutes, int seconds, string expected)
    {
        var uptime = new TimeSpan(days, hours, minutes, seconds);

        Assert.Equal(expected, SystemInfoService.FormatUptime(uptime));
    }

    [Fact]
    public void FormatUptime_ClampsNegativeValues()
    {
        Assert.Equal("0 分 0 秒", SystemInfoService.FormatUptime(TimeSpan.FromSeconds(-5)));
    }

    [Fact]
    public void Collect_ReturnsCoreGroups()
    {
        var groups = new SystemInfoService().Collect();
        var titles = groups.Select(group => group.Title).ToList();

        Assert.Contains("操作系统", titles);
        Assert.Contains("计算机", titles);
        Assert.Contains("处理器", titles);
        Assert.Contains("内存", titles);
        Assert.Contains("磁盘", titles);
        Assert.All(groups, group => Assert.NotEmpty(group.Items));
    }
}
