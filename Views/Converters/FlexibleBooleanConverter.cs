using System;
using System.Globalization;
using System.Windows.Data;

namespace PautaDinamicaApp.Views.Converters
{
    public class FlexibleBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return false;

            if (value is bool b) return b;

            string s = value.ToString()?.ToLower() ?? "";

            if (s == "true" || s == "1" || s == "si" || s == "sí" || s == "yes" || s == "cumple")
                return true;

            if (s == "false" || s == "0" || s == "no" || s == "no cumple")
                return false;

            return false; // Fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? "True" : "False";
            return "False";
        }
    }
}
