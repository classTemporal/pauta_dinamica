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
            if (value == null || parameter == null) return System.Windows.Data.Binding.DoNothing;

            string input = value.ToString() ?? "";
            string param = parameter.ToString() ?? "";

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
            if (value is bool b && !b) return System.Windows.Data.Binding.DoNothing;
            if (parameter == null) return System.Windows.Data.Binding.DoNothing;

            string targetVal = value?.ToString() ?? "";
            string param = parameter.ToString() ?? "";

            var mappings = param.Split('|')
                .Select(m => m.Split(':'))
                .Where(m => m.Length == 2)
                .ToDictionary(m => m[0].Trim(), m => m[1].Trim());

            var match = mappings.FirstOrDefault(x => x.Value.Equals(targetVal, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Key))
            {
                return match.Key;
            }

            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
