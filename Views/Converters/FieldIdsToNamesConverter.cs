using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using PautaDinamicaApp.Models; // Asegúrate de tener el namespace correcto para FieldDefinition

namespace PautaDinamicaApp.Views.Converters
{
    public class FieldIdsToNamesConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return "";

            var selectedIds = values[0] as ICollection<string>;
            var availableFields = values[1] as IEnumerable<FieldDefinition>;

            if (selectedIds == null || selectedIds.Count == 0) return "Ningún campo seleccionado";
            if (availableFields == null) return $"{selectedIds.Count} campos (IDs)";

            var names = availableFields
                .Where(f => selectedIds.Contains(f.Id))
                .Select(f => f.Label)
                .ToList();

            if (names.Count == 0) return "Campos desconocidos";

            if (names.Count == 1) return names[0];

            return $"{names.Count} campos seleccionados";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
