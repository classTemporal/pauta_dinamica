using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace PautaDinamicaApp.Views.Converters
{
    public class PrependOptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var list = value as IEnumerable;
            string option = parameter as string ?? "(Ninguno)";

            var result = new List<object> { string.Empty }; // El valor real sera vacio para "Ninguno"

            if (list != null)
            {
                foreach (var item in list)
                    result.Add(item);
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
