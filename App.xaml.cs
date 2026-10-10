using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PautaDinamicaApp.Services;

namespace PautaDinamicaApp
{
    public partial class App : System.Windows.Application
    {
        public App()
        {
            // Suppress ComboBox selection change on page scroll: when the mouse
            // is over a ComboBox, mark the PreviewMouseWheel event as handled so
            // that the scroll wheel does not change the selected value.
            EventManager.RegisterClassHandler(
                typeof(System.Windows.Controls.ComboBox),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnPreviewMouseWheel));
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load and apply theme
            var storage = new StorageService();
            var settings = storage.LoadSettings();
            var themeService = new ThemeService();

            themeService.SetTheme(settings.Theme);
            themeService.ApplyAccentColor(settings.AccentColor);

            // Ensure all future windows apply the correct title bar theme.
            // El tema se aplica en el evento Loaded leyendo el HWND existente (nunca con
            // EnsureHandle): forzar la creación del handle en mitad de la carga del BAML de la
            // ventana inicial provocaba al arrancar
            //   "No se puede cambiar AllowsTransparency después de mostrarse un elemento Window
            //    o de haber llamado a WindowInteropHelper.EnsureHandle".
            EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler((s, args) =>
            {
                if (s is Window window)
                {
                    themeService.ApplyThemeToWindow(window, ThemeService.CurrentTheme);
                }
            }));

            // La ventana de inicio (LoginWindow) se crea después de OnStartup cargando su BAML:
            // en su Loaded todavía no existe HWND, así que se cubre con una pasada diferida que
            // se ejecuta cuando el arranque ya terminó.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (Window window in Windows)
                {
                    themeService.ApplyThemeToWindow(window, ThemeService.CurrentTheme);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // The mouse is over a ComboBox when this handler is reached.
            // No cambiar la selección con la rueda cuando el desplegable está
            // cerrado, PERO reenviar el scroll al ScrollViewer padre para que
            // la página siga desplazándose bajo el cursor (antes se tragaba
            // el evento con e.Handled=true y la página quedaba clavada).
            if (sender is System.Windows.Controls.ComboBox combo)
            {
                if (combo.IsDropDownOpen) return;
                e.Handled = true;
                var parent = FindParentScrollViewer(combo);
                if (parent != null)
                {
                    var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                    {
                        RoutedEvent = UIElement.MouseWheelEvent,
                        Source = parent
                    };
                    parent.RaiseEvent(forwarded);
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        private static System.Windows.Controls.ScrollViewer? FindParentScrollViewer(DependencyObject child)
        {
            DependencyObject current = child;
            while (VisualTreeHelper.GetParent(current) is DependencyObject parent)
            {
                if (parent is System.Windows.Controls.ScrollViewer sv) return sv;
                current = parent;
            }
            return null;
        }
    }
}
