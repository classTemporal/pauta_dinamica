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

            if (isMissing)
            {
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
            }

            // When not missing, use the theme-aware TextBrush so the text is legible
            // in both light and dark themes (previously returned pure black #000000,
            // which was invisible in dark mode).
            if (System.Windows.Application.Current?.Resources["TextBrush"] is SolidColorBrush textBrush)
            {
                return textBrush;
            }

            // Fallback: light gray that is readable on dark backgrounds
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE1, 0xE1, 0xE1));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}