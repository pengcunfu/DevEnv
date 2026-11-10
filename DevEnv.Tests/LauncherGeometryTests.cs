using DevEnv.Core.Services;

namespace DevEnv.Tests;

public class LauncherGeometryTests
{
    // 本机实测：2560 × 1440 @125%，任务栏 60 物理像素 → 工作区 2048 × 1104 DIP
    private const double WorkWidth = 2048;
    private const double WorkHeight = 1104;

    [Fact]
    public void Compute_UsesFixedSize()
    {
        var bounds = LauncherGeometry.Compute(0, 0, WorkWidth, WorkHeight);

        Assert.Equal(960, bounds.Width);
        Assert.Equal(680, bounds.Height);
    }

    [Fact]
    public void Compute_CentersBothAxes()
    {
        var bounds = LauncherGeometry.Compute(0, 0, WorkWidth, WorkHeight);

        Assert.Equal((WorkWidth - LauncherGeometry.Width) / 2, bounds.Left);
        Assert.Equal((WorkHeight - LauncherGeometry.Height) / 2, bounds.Top);
        // 左右、上下留白相等
        Assert.Equal(bounds.Left, WorkWidth - (bounds.Left + bounds.Width));
        Assert.Equal(bounds.Top, WorkHeight - (bounds.Top + bounds.Height));
    }

    [Fact]
    public void Compute_RespectsWorkAreaOffset()
    {
        var bounds = LauncherGeometry.Compute(-1920, 100, WorkWidth, WorkHeight);

        Assert.Equal(-1920 + (WorkWidth - LauncherGeometry.Width) / 2, bounds.Left);
        Assert.Equal(100 + (WorkHeight - LauncherGeometry.Height) / 2, bounds.Top);
    }

    [Fact]
    public void Compute_SizeStaysFixedRegardlessOfWorkArea()
    {
        var small = LauncherGeometry.Compute(0, 0, 1024, 768);
        var large = LauncherGeometry.Compute(0, 0, 3840, 2080);

        Assert.Equal(LauncherGeometry.Width, small.Width);
        Assert.Equal(LauncherGeometry.Height, small.Height);
        Assert.Equal(LauncherGeometry.Width, large.Width);
        Assert.Equal(LauncherGeometry.Height, large.Height);
    }
}
