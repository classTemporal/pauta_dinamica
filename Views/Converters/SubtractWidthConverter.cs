using System;
using System.Globalization;
using System.Windows.Data;

namespace PautaDinamicaApp.Views.Converters
{
    /// <summary>
    /// Resta una cantidad fija (ConverterParameter) a un ancho entrante.
    /// Se usa cuando un elemento debe ocupar "todo el ancho disponible" pero además tiene
    /// márgenes laterales: sin restarlos, el elemento desborda y queda pegado a los bordes
    /// o se recorta (p. ej. el campo de texto largo del dashboard).
    /// </summary>
    public class SubtractWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double amount = 0;
            if (parameter != null)
                double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out amount);

            double width = value switch
            {
                double d => d,
                int i => i,
                float f => f,
                _ => 0
            };

            double result = width - amount;
            return result > 0 ? result : 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
