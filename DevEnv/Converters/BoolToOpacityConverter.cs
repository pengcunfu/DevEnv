using System.Globalization;
using System.Windows.Data;

namespace DevEnv.Host.Converters;

/// <summary>布尔 → 不透明度：true 用 Active（默认 1），false 用 Inactive（默认 0.28）。</summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public double Active { get; set; } = 1.0;
    public double Inactive { get; set; } = 0.28;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        return flag ? Active : Inactive;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
