using System;
using System.Globalization;
using System.Windows.Data;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views.Converters
{
    /// <summary>
    /// Muestra el valor predeterminado de fecha de forma legible en el diseñador:
    /// TODAY → "Hoy (dinámico)", MONTHDAY:N → "Día N de cada mes", resto tal cual.
    /// </summary>
    public class DateDefaultDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => DateDefaultValue.Describe(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
