using DevEnv.Core.Services;

namespace DevEnv.Tests;

public class LauncherGeometryTests
{
    // 本机实测：2560 × 1440 @125%，任务栏 60 物理像素 → 工作区 2048 × 1104 DIP
    private const double WorkWidth = 2048;
    private const double WorkHeight = 1104;

    [Fact]
    public void Compute_LeftAlignedTaskbar_MatchesStartMenu()
    {
        var bounds = LauncherGeometry.Compute(0, 0, WorkWidth, WorkHeight, taskbarCentered: false);

        // 与开始菜单实测值一致：左边距 13 DIP，832 × 862 DIP，底边距任务栏 14 DIP
        Assert.Equal(LauncherGeometry.LeftInset, bounds.Left);
        Assert.Equal(LauncherGeometry.Width, bounds.Width);
        Assert.Equal(LauncherGeometry.Height, bounds.Height);
        Assert.Equal(WorkHeight - LauncherGeometry.Height - LauncherGeometry.BottomInset, bounds.Top);
    }

    [Fact]
    public void Compute_CenteredTaskbar_CentersHorizontally()
    {
        var bounds = LauncherGeometry.Compute(0, 0, WorkWidth, WorkHeight, taskbarCentered: true);

        Assert.Equal((WorkWidth - LauncherGeometry.Width) / 2, bounds.Left);
        Assert.Equal(LauncherGeometry.Width, bounds.Width);
    }

    [Fact]
    public void Compute_RespectsWorkAreaOffset()
    {
        var bounds = LauncherGeometry.Compute(-1920, 100, WorkWidth, WorkHeight, taskbarCentered: false);

        Assert.Equal(-1920 + LauncherGeometry.LeftInset, bounds.Left);
        Assert.Equal(100 + WorkHeight - LauncherGeometry.Height - LauncherGeometry.BottomInset, bounds.Top);
    }

    [Fact]
    public void Compute_ShrinksOnShortWorkArea_WithoutOverflow()
    {
        const double shortHeight = 700;
        var bounds = LauncherGeometry.Compute(0, 40, WorkWidth, shortHeight, taskbarCentered: false);

        Assert.True(bounds.Top >= 40);
        Assert.True(bounds.Top + bounds.Height <= 40 + shortHeight - LauncherGeometry.BottomInset);
        Assert.True(bounds.Height < LauncherGeometry.Height);
    }

    [Fact]
    public void Compute_ShrinksOnNarrowWorkArea_WithoutOverflow()
    {
        const double narrowWidth = 900;
        var bounds = LauncherGeometry.Compute(0, 0, narrowWidth, WorkHeight, taskbarCentered: false);

        Assert.True(bounds.Left >= 0);
        Assert.True(bounds.Left + bounds.Width <= narrowWidth);
    }
}
