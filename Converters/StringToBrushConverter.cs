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
                    "green" => new SolidColorBrush(Colors.Green),
                    "red" => new SolidColorBrush(Colors.Red),
                    "blue" => new SolidColorBrush(Colors.Blue),
                    "orange" => new SolidColorBrush(Colors.Orange),
                    "gray" => new SolidColorBrush(Colors.Gray),
                    _ => new SolidColorBrush(Colors.Black)
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