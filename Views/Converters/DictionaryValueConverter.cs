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
                    if (val == null) return "";

                    // Desempaquetar JsonElement si es necesario
                    if (val is System.Text.Json.JsonElement element)
                    {
                        switch (element.ValueKind)
                        {
                            case System.Text.Json.JsonValueKind.True: return "1";
                            case System.Text.Json.JsonValueKind.False: return "0";
                            case System.Text.Json.JsonValueKind.String: val = element.GetString(); break;
                            case System.Text.Json.JsonValueKind.Number: val = element.GetDouble(); break;
                            default: val = element.ToString(); break;
                        }
                    }

                    // Manejar Booleanos nativos o Strings que parecen booleanos
                    if (val is bool b) return b ? "1" : "0";
                    if (val.ToString().Equals("True", StringComparison.OrdinalIgnoreCase)) return "1";
                    if (val.ToString().Equals("False", StringComparison.OrdinalIgnoreCase)) return "0";

                    return val;
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
