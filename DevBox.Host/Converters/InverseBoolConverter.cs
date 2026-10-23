using System.Globalization;
using System.Windows.Data;

namespace DevBox.Host.Converters;

/// <summary>布尔取反，用于「文件 / 文本」这类互斥单选。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}