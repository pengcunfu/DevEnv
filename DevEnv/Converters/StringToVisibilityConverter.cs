using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DevEnv.Host.Converters;

/// <summary>非空字符串 → Visible（Invert=true 时相反），用于「有图标就不显示文字占位」。</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasText = !string.IsNullOrWhiteSpace(value as string);
        var visible = Invert ? !hasText : hasText;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}