namespace DevEnv.Core.Services;

/// <summary>
/// 全局启动器与空格预览浮窗的屏幕几何：固定 960 × 680 DIP，在工作区内水平、垂直居中。
///
/// 工作区已扣除任务栏，居中时不会压到任务栏。纯函数，不依赖 WPF / 注册表，便于单测。
/// </summary>
public static class LauncherGeometry
{
    /// <summary>启动器 / 空格预览共用的固定宽度（DIP）。</summary>
    public const double Width = 960;

    /// <summary>启动器 / 空格预览共用的固定高度（DIP）。</summary>
    public const double Height = 680;

    /// <summary>屏幕几何（DIP），坐标系与 <c>SystemParameters.WorkArea</c> 一致。</summary>
    public readonly record struct Bounds(double Left, double Top, double Width, double Height);

    /// <summary>计算启动器应占的矩形：固定 960 × 680 DIP，水平、垂直居中。</summary>
    public static Bounds Compute(
        double workLeft,
        double workTop,
        double workWidth,
        double workHeight)
    {
        return new Bounds(
            workLeft + (workWidth - Width) / 2,
            workTop + (workHeight - Height) / 2,
            Width,
            Height);
    }
}
