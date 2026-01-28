using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace PautaDinamicaApp.Views.Converters
{
    public class DictionaryValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IDictionary<string, object> dict && parameter is string key)
            {
                if (dict.TryGetValue(key, out var val))
                {
                    return val ?? "";
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
