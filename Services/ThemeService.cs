using System;
using System.Linq;
using System.Windows;

namespace PautaDinamicaApp.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public class ThemeService
    {
        private const string DarkThemePath = "Views/Resources/Themes/DarkTheme.xaml";
        private const string LightThemePath = "Views/Resources/Themes/LightTheme.xaml";

        public void SetTheme(AppTheme theme)
        {
            string themePath = theme == AppTheme.Dark ? DarkThemePath : LightThemePath;
            var newResourceDict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };

            var mergedDicts = System.Windows.Application.Current.Resources.MergedDictionaries;

            // Look for existing theme dictionary and replace it
            var existingThemeDict = mergedDicts.FirstOrDefault(d =>
                d.Source != null && (d.Source.OriginalString.Contains("DarkTheme.xaml") || d.Source.OriginalString.Contains("LightTheme.xaml")));

            if (existingThemeDict != null)
            {
                int index = mergedDicts.IndexOf(existingThemeDict);
                mergedDicts[index] = newResourceDict;
            }
            else
            {
                // If not found, insert at the beginning to ensure other styles can override if needed
                mergedDicts.Insert(0, newResourceDict);
            }
        }
    }
}
