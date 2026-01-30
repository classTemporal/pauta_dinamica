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
            new ThemeService().SetTheme(settings.Theme);
        }
    }
}
