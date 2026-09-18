using System.Windows;
using PautaDinamicaApp.Services;

namespace PautaDinamicaApp
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load and apply theme
            var storage = new StorageService();
            var settings = storage.LoadSettings();
            var themeService = new ThemeService();

            themeService.SetTheme(settings.Theme);
            themeService.ApplyAccentColor(settings.AccentColor);

            // Ensure all future windows apply the correct title bar theme when loaded
            EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler((s, args) =>
            {
                if (s is Window window)
                {
                    themeService.ApplyThemeToWindow(window, ThemeService.CurrentTheme);
                }
            }));
        }
    }
}
