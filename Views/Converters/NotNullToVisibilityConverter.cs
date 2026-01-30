using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PautaDinamicaApp.Views.Converters
{
    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNotNull = value != null;
            if (value is string s && string.IsNullOrEmpty(s)) isNotNull = false;

            bool inverse = parameter?.ToString() == "Inverse";

            if (inverse) return isNotNull ? Visibility.Collapsed : Visibility.Visible;
            return isNotNull ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
