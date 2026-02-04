using System;
using System.Globalization;
using System.Windows.Data;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views.Converters
{
    public class CalculationRoundingDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CalculationRounding mode)
            {
                return mode switch
                {
                    CalculationRounding.None => "Desactivado (Estándar)",
                    CalculationRounding.Up => "Redondear hacia arriba (Techo)",
                    CalculationRounding.Down => "Redondear hacia abajo (Piso)",
                    _ => mode.ToString()
                };
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
