using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

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
        private const string AccentColorPath = "Views/Resources/Themes/AccentColor.xaml";

        public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

        private const string DefaultAccentColor = "#007bff";

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public void SetTheme(AppTheme theme)
        {
            CurrentTheme = theme;
            string themePath = theme == AppTheme.Dark ? DarkThemePath : LightThemePath;
            var newResourceDict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };

            var mergedDicts = System.Windows.Application.Current.Resources.MergedDictionaries;

            var existingThemeDict = mergedDicts.FirstOrDefault(d =>
                d.Source != null && (d.Source.OriginalString.Contains("DarkTheme.xaml") || d.Source.OriginalString.Contains("LightTheme.xaml")));

            if (existingThemeDict != null)
            {
                int index = mergedDicts.IndexOf(existingThemeDict);
                mergedDicts[index] = newResourceDict;
            }
            else
            {
                mergedDicts.Insert(0, newResourceDict);
            }

            // Apply to all open windows
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                ApplyThemeToWindow(window, theme);
            }
        }

        public void ApplyAccentColor(string colorHex)
        {
            var mergedDicts = System.Windows.Application.Current.Resources.MergedDictionaries;

            var existingAccentDict = mergedDicts.FirstOrDefault(d =>
                d.Source != null && d.Source.OriginalString.Contains("AccentColor.xaml"));

            if (existingAccentDict is ResourceDictionary dict)
            {
                // Replace the AccentBrush value in the existing dictionary
                dict["AccentBrush"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));
            }
            else
            {
                // Insert a new dictionary with the accent brush at the end (after theme)
                var newAccentDict = new ResourceDictionary();
                newAccentDict.Add("AccentBrush", new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)));
                mergedDicts.Add(newAccentDict);
            }
        }

        public void ApplyThemeToWindow(Window window, AppTheme theme)
        {
            if (window == null) return;

            IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();
            int useImmersiveDarkMode = theme == AppTheme.Dark ? 1 : 0;

            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useImmersiveDarkMode, sizeof(int));
        }

        public static AppTheme GetSystemTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object? value = key.GetValue("AppsUseLightTheme");
                        if (value is int lightThemeValue)
                        {
                            return lightThemeValue == 0 ? AppTheme.Dark : AppTheme.Light;
                        }
                    }
                }
            }
            catch { }
            return AppTheme.Light; // Default
        }

        public static string GetDefaultAccentColor() => DefaultAccentColor;
    }
}
