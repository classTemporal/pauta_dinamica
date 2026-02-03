using System;
using System.Globalization;
using System.Windows.Data;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views.Converters
{
    public class FieldTypeDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FieldType type)
            {
                return type switch
                {
                    FieldType.Text => "Texto corto",
                    FieldType.Numeric => "Número",
                    FieldType.Date => "Fecha",
                    FieldType.Time => "Hora",
                    FieldType.Dropdown => "Lista de elementos",
                    FieldType.Calculation => "Porcentaje",
                    FieldType.Boolean => "Binario",
                    FieldType.Average => "Promedio",
                    FieldType.TextArea => "Texto largo",
                    FieldType.Separator => "--- SECCIÓN ---",
                    _ => type.ToString()
                };
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "Texto corto" => FieldType.Text,
                    "Número" => FieldType.Numeric,
                    "Fecha" => FieldType.Date,
                    "Hora" => FieldType.Time,
                    "Lista de elementos" => FieldType.Dropdown,
                    "Porcentaje" => FieldType.Calculation,
                    "Binario" => FieldType.Boolean,
                    "Promedio" => FieldType.Average,
                    "Texto largo" => FieldType.TextArea,
                    "--- SECCIÓN ---" => FieldType.Separator,
                    _ => Enum.TryParse(typeof(FieldType), str, out var result) ? result : FieldType.Text
                };
            }
            return FieldType.Text;
        }
    }
}
