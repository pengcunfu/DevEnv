using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DevEnv.Converters;

/// <summary>
/// Converts bool to IsVisible (Avalonia uses bool instead of WPF Visibility enum).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue;

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue;

        return false;
    }
}
