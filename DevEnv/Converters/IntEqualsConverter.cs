using System.Globalization;
using System.Windows.Data;

namespace DevEnv.Host.Converters;

/// <summary>整型与 ConverterParameter 比较；用于 RadioButton 绑定枚举 / 索引。</summary>
public sealed class IntEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!TryParse(parameter, out var expected))
            return false;
        return value is int actual && actual == expected;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && TryParse(parameter, out var expected))
            return expected;
        return Binding.DoNothing;
    }

    private static bool TryParse(object? parameter, out int value)
    {
        value = 0;
        return parameter is not null
            && int.TryParse(parameter.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
