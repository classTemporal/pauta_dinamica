using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace PautaDinamicaApp.Views.Converters
{
    public class StringToLanguageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string lang && !string.IsNullOrEmpty(lang))
            {
                try
                {
                    return XmlLanguage.GetLanguage(lang);
                }
                catch
                {
                    return XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
                }
            }
            return XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
