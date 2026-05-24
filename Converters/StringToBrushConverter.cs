using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DevEnv.Converters;

public class StringToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string colorString)
        {
            return colorString.ToLower() switch
            {
                "green" => new SolidColorBrush(Color.Parse("#059669")),
                "red" => new SolidColorBrush(Color.Parse("#DC2626")),
                "blue" => new SolidColorBrush(Color.Parse("#2563EB")),
                "orange" => new SolidColorBrush(Color.Parse("#D97706")),
                "gray" => new SolidColorBrush(Color.Parse("#6B7280")),
                "dodgerblue" => new SolidColorBrush(Color.Parse("#0284C7")),
                _ => new SolidColorBrush(Color.Parse("#374151"))
            };
        }

        return Brushes.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
