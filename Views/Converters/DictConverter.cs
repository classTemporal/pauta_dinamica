using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Linq;

namespace PautaDinamicaApp.Views.Converters
{
    public class DictConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return DependencyProperty.UnsetValue;

            string input = value.ToString() ?? "";
            string param = parameter.ToString() ?? "";

            // Formato: "Key1:Value1|Key2:Value2|Default:ValueDefault"
            var mappings = param.Split('|')
                .Select(m => m.Split(':'))
                .Where(m => m.Length == 2)
                .ToDictionary(m => m[0].Trim(), m => m[1].Trim());

            string resultStr = mappings.ContainsKey(input) ? mappings[input] : (mappings.ContainsKey("Default") ? mappings["Default"] : "");

            if (targetType == typeof(Visibility))
            {
                return Enum.TryParse(resultStr, out Visibility v) ? v : Visibility.Visible;
            }

            return resultStr;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
