using System.Globalization;
using DevEnv.Core.ColorTools.Models;

namespace DevEnv.Core.ColorTools.Services;

/// <summary>
/// 颜色解析与转换：支持 #RGB / #RGBA / #RRGGBB / #AARRGGBB、rgb() / rgba()、hsl() / hsla()，
/// 以及 HEX / RGB / HSL 之间的互转与明暗色阶生成。
/// </summary>
public static class ColorToolkit
{
    /// <summary>
    /// 解析颜色文本。8 位十六进制按 Windows / WPF 习惯视为 #AARRGGBB。
    /// </summary>
    public static bool TryParse(string? text, out ColorValue color, out string? error)
    {
        color = new ColorValue(255, 0, 0, 0);
        error = null;

        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            error = "请输入颜色值";
            return false;
        }

        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            return TryParseRgb(value, out color, out error);

        if (value.StartsWith("hsl", StringComparison.OrdinalIgnoreCase))
            return TryParseHsl(value, out color, out error);

        return TryParseHex(value, out color, out error);
    }

    /// <summary>色相 0~360，饱和度与亮度 0~100。</summary>
    public static (double H, double S, double L) ToHsl(ColorValue color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;
        var delta = max - min;

        if (delta <= double.Epsilon)
            return (0, 0, lightness * 100);

        var saturation = lightness > 0.5
            ? delta / (2 - max - min)
            : delta / (max + min);

        double hue;
        if (max == r)
            hue = (g - b) / delta + (g < b ? 6 : 0);
        else if (max == g)
            hue = (b - r) / delta + 2;
        else
            hue = (r - g) / delta + 4;

        return (hue * 60, saturation * 100, lightness * 100);
    }

    /// <summary>由 HSL 生成颜色；色相取模到 0~360，饱和度与亮度会被限制在 0~100。</summary>
    public static ColorValue FromHsl(double h, double s, double l, byte a = 255)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 100) / 100.0;
        l = Math.Clamp(l, 0, 100) / 100.0;

        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
        var m = l - c / 2;

        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };

        return new ColorValue(a, ToByte((r + m) * 255), ToByte((g + m) * 255), ToByte((b + m) * 255));
    }

    public static string HslText(ColorValue color)
    {
        var (h, s, l) = ToHsl(color);
        return FormattableString.Invariant($"hsl({h:0.##}, {s:0.##}%, {l:0.##}%)");
    }

    /// <summary>按当前色相与饱和度生成由浅到深的色板，亮度从 92% 均匀降到 12%。</summary>
    public static IReadOnlyList<ColorValue> GetPalette(ColorValue baseColor, int count = 9)
    {
        count = Math.Clamp(count, 2, 21);
        var (h, s, _) = ToHsl(baseColor);

        var palette = new List<ColorValue>(count);
        for (var i = 0; i < count; i++)
        {
            var lightness = 92 - i * (80.0 / (count - 1));
            palette.Add(FromHsl(h, s, lightness, baseColor.A));
        }

        return palette;
    }

    private static bool TryParseHex(string value, out ColorValue color, out string? error)
    {
        color = new ColorValue(255, 0, 0, 0);
        error = null;

        var hex = value.TrimStart('#');
        if (hex.Length is not (3 or 4 or 6 or 8))
        {
            error = "HEX 颜色应为 3 / 4 / 6 / 8 位，例如 #3B82F6";
            return false;
        }

        foreach (var c in hex)
        {
            if (!Uri.IsHexDigit(c))
            {
                error = "HEX 颜色只能包含 0-9 与 A-F";
                return false;
            }
        }

        byte a = 255;
        switch (hex.Length)
        {
            case 3:
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
                break;
            case 4:
                // #RGBA：A 在末尾
                var alpha = hex[3];
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
                a = ParseByte($"{alpha}{alpha}");
                break;
            case 8:
                // #AARRGGBB：A 在最前
                a = ParseByte(hex[..2]);
                hex = hex[2..];
                break;
        }

        color = new ColorValue(
            a,
            ParseByte(hex[..2]),
            ParseByte(hex[2..4]),
            ParseByte(hex[4..6]));
        return true;
    }

    private static bool TryParseRgb(string value, out ColorValue color, out string? error)
    {
        color = new ColorValue(255, 0, 0, 0);
        if (!TryGetArguments(value, out var parts, out error))
            return false;

        if (parts.Length is not (3 or 4))
        {
            error = "rgb() 需要 3 个颜色分量，rgba() 可再跟一个透明度";
            return false;
        }

        if (!TryParseChannel(parts[0], out var r, out error)
            || !TryParseChannel(parts[1], out var g, out error)
            || !TryParseChannel(parts[2], out var b, out error))
        {
            return false;
        }

        var a = (byte)255;
        if (parts.Length == 4)
        {
            if (!TryParseAlpha(parts[3], out a, out error))
                return false;
        }

        color = new ColorValue(a, r, g, b);
        return true;
    }

    private static bool TryParseHsl(string value, out ColorValue color, out string? error)
    {
        color = new ColorValue(255, 0, 0, 0);
        if (!TryGetArguments(value, out var parts, out error))
            return false;

        if (parts.Length is not (3 or 4))
        {
            error = "hsl() 需要 3 个分量（色相 / 饱和度 / 亮度），可再跟一个透明度";
            return false;
        }

        var hueText = parts[0].TrimEnd('%');
        if (hueText.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
            hueText = hueText[..^3];

        if (!TryParseDouble(hueText, out var hue)
            || !TryParseDouble(parts[1].TrimEnd('%'), out var saturation)
            || !TryParseDouble(parts[2].TrimEnd('%'), out var lightness))
        {
            error = "HSL 分量必须是数字";
            return false;
        }

        var a = (byte)255;
        if (parts.Length == 4)
        {
            if (!TryParseAlpha(parts[3], out a, out error))
                return false;
        }

        color = FromHsl(hue, saturation, lightness, a);
        return true;
    }

    private static bool TryGetArguments(string value, out string[] parts, out string? error)
    {
        parts = [];
        error = null;

        var open = value.IndexOf('(');
        var close = value.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            error = "颜色函数缺少括号";
            return false;
        }

        var inner = value[(open + 1)..close].Replace('/', ' ');
        parts = inner.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            error = "颜色函数缺少分量";
            return false;
        }

        return true;
    }

    private static bool TryParseChannel(string text, out byte channel, out string? error)
    {
        channel = 0;
        error = null;

        if (text.EndsWith('%'))
        {
            if (!TryParseDouble(text.TrimEnd('%'), out var percent))
            {
                error = "颜色分量必须是数字";
                return false;
            }

            channel = ToByte(percent / 100 * 255);
            return true;
        }

        if (!TryParseDouble(text, out var number))
        {
            error = "颜色分量必须是数字";
            return false;
        }

        channel = ToByte(number);
        return true;
    }

    private static bool TryParseAlpha(string text, out byte alpha, out string? error)
    {
        alpha = 255;
        error = null;

        if (text.EndsWith('%'))
        {
            if (!TryParseDouble(text.TrimEnd('%'), out var percent))
            {
                error = "透明度必须是数字";
                return false;
            }

            alpha = ToByte(percent / 100 * 255);
            return true;
        }

        if (!TryParseDouble(text, out var number))
        {
            error = "透明度必须是数字";
            return false;
        }

        alpha = number <= 1
            ? ToByte(number * 255)
            : ToByte(number);
        return true;
    }

    private static bool TryParseDouble(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static byte ParseByte(string hex) =>
        byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static byte ToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
}
