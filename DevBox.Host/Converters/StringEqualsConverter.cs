using System.Globalization;
using System.Windows.Data;

namespace DevBox.Host.Converters;

/// <summary>字符串相等比较（值 vs ConverterParameter），用于「当前选中项」这类单选高亮。</summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? parameter as string ?? string.Empty : Binding.DoNothing;
}