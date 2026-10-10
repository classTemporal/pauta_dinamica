using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PautaDinamicaApp.Views.Converters
{
    /// <summary>
    /// Muestra el tag de categoría solo cuando hay una categoría real asignada.
    /// Oculta el tag para null, vacío y "No categorizado" (valor interno por defecto).
    /// Acepta ConverterParameter=Inverse para invertir la lógica.
    /// </summary>
    public class CategoryToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string category = (value as string ?? string.Empty).Trim();
            bool hasCategory = !string.IsNullOrEmpty(category)
                && !category.Equals("No categorizado", StringComparison.OrdinalIgnoreCase);

            bool inverse = string.Equals(parameter?.ToString(), "Inverse", StringComparison.OrdinalIgnoreCase);

            if (inverse) return hasCategory ? Visibility.Collapsed : Visibility.Visible;
            return hasCategory ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
