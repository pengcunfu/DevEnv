namespace DevEnv.Core.Services;

/// <summary>
/// 全局启动器的屏幕几何：与 Windows 11 开始菜单保持同等大小、同等位置。
///
/// 尺寸取自 Windows 11（build 26200，125% 缩放）开始菜单的实测值
/// 1040 × 1078 物理像素，按 120 DPI 换算成 832 × 862 DIP；
/// 位置贴着工作区底部：底边距任务栏 14 DIP。任务栏居中时水平居中，
/// 左对齐时左边距 13 DIP，与开始菜单的贴边位置一致。
///
/// 纯函数，不依赖 WPF / 注册表，便于单测；任务栏对齐方式由调用方读取后传入。
/// </summary>
public static class LauncherGeometry
{
    /// <summary>开始菜单实测宽度（DIP）。</summary>
    public const double Width = 832;

    /// <summary>开始菜单实测高度（DIP）。</summary>
    public const double Height = 862;

    /// <summary>任务栏左对齐时，开始菜单距工作区左边缘的距离（DIP）。</summary>
    public const double LeftInset = 13;

    /// <summary>开始菜单底边距工作区底边的距离（DIP）。</summary>
    public const double BottomInset = 14;

    /// <summary>可用高度不足时，顶部至少留出的距离（DIP）。</summary>
    public const double TopInset = 13;

    /// <summary>屏幕过小时允许收缩到的最小边长（DIP）。</summary>
    private const double MinimumSize = 320;

    /// <summary>屏幕几何（DIP），坐标系与 <c>SystemParameters.WorkArea</c> 一致。</summary>
    public readonly record struct Bounds(double Left, double Top, double Width, double Height);

    /// <summary>
    /// 计算启动器应占的矩形。<paramref name="taskbarCentered"/> 为 true 表示
    /// 任务栏图标居中（Windows 11 默认），false 表示左对齐。
    /// </summary>
    public static Bounds Compute(
        double workLeft,
        double workTop,
        double workWidth,
        double workHeight,
        bool taskbarCentered)
    {
        // 屏幕特别小的时候先保证窗口不超出工作区，再尽量保留开始菜单的尺寸
        var width = Math.Min(Width, Math.Max(MinimumSize, workWidth - LeftInset * 2));
        var height = Math.Min(Height, Math.Max(MinimumSize, workHeight - TopInset - BottomInset));
        if (height > workHeight - BottomInset)
            height = Math.Max(1, workHeight - BottomInset);

        var left = taskbarCentered
            ? workLeft + (workWidth - width) / 2
            : workLeft + LeftInset;
        left = Math.Max(workLeft, left);

        var top = Math.Max(workTop + TopInset, workTop + workHeight - BottomInset - height);
        return new Bounds(left, top, width, height);
    }
}
