namespace DevEnv.Host.SigXor.Services;

/// <summary>
/// 文本预览的会话级字号缩放。Ctrl + 鼠标滚轮修改后，后续打开的文本 / 源码预览沿用同一字号；
/// 在设置里保存新的默认字号时重置，重新回到设置值。
/// </summary>
public static class PreviewTextZoom
{
    public const double MinFontSize = 8;
    public const double MaxFontSize = 72;

    private static double? _size;

    /// <summary>当前缩放字号；未缩放过时为 null，表示使用设置里的默认字号。</summary>
    public static double? Size => _size;

    public static double Resolve(double settingsFontSize) =>
        _size ?? Math.Clamp(settingsFontSize, MinFontSize, MaxFontSize);

    /// <summary>设置缩放字号并返回钳制后的值。</summary>
    public static double Set(double size)
    {
        _size = Math.Clamp(size, MinFontSize, MaxFontSize);
        return _size.Value;
    }

    public static void Reset() => _size = null;
}
