using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DevEnv.Converters
{
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString)
            {
                return colorString.ToLower() switch
                {
                    "green" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669")!),
                    "red" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")!),
                    "blue" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")!),
                    "orange" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706")!),
                    "gray" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")!),
                    "dodgerblue" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7")!),
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")!)
                };
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}