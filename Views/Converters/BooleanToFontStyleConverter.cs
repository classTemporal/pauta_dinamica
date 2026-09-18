using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PautaDinamicaApp.Views.Converters
{
    public class BooleanToFontStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isMissing = (value is bool b) ? b : false;
            bool isInverse = parameter?.ToString() == "Inverse";
            if (isInverse) isMissing = !isMissing;
            return isMissing ? FontStyles.Italic : FontStyles.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
