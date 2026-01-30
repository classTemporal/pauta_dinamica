using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PautaDinamicaApp.Views.Converters
{
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string colorStr && !string.IsNullOrWhiteSpace(colorStr))
            {
                try
                {
                    var converter = new BrushConverter();
                    var brush = converter.ConvertFromString(colorStr) as System.Windows.Media.Brush;
                    return brush ?? System.Windows.Media.Brushes.Transparent;
                }
                catch
                {
                    return System.Windows.Media.Brushes.Transparent;
                }
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
