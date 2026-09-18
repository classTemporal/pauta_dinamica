using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PautaDinamicaApp.Views.Converters
{
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isMissing = (value is bool b) ? b : false;
            bool isInverse = parameter?.ToString() == "Inverse";
            if (isInverse) isMissing = !isMissing;
            return isMissing ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
