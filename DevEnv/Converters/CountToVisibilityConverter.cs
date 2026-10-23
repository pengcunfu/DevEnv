using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DevEnv.Host.Converters;

/// <summary>集合数量 → 可见性。count &gt; 0 时为 Visible（Invert=true 时相反）。</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        var visible = count > 0;
        if (Invert)
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}