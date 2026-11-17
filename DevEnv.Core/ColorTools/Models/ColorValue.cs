using System.Globalization;

namespace DevEnv.Core.ColorTools.Models;

/// <summary>与界面无关的 RGBA 颜色值。</summary>
public readonly record struct ColorValue(byte A, byte R, byte G, byte B)
{
    /// <summary>不透明时输出 #RRGGBB，带透明度时输出 #AARRGGBB。</summary>
    public string Hex => A == 255
        ? $"#{R:X2}{G:X2}{B:X2}"
        : $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    public string RgbText => A == 255
        ? FormattableString.Invariant($"rgb({R}, {G}, {B})")
        : FormattableString.Invariant($"rgba({R}, {G}, {B}, {A / 255.0:0.##})");

    public override string ToString() => Hex;
}
