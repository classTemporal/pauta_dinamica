using Microsoft.Web.WebView2.Core;

namespace PautaDinamicaApp.Services
{
    /// <summary>
    /// Detecta si el runtime de WebView2 (Edge Chromium) está disponible en el equipo.
    /// Viene preinstalado en Windows 11 / Windows 10 actualizado / con Office 365,
    /// pero falta en LTSC, equipos sin actualizar u offline: por eso el editor
    /// usa WebView2 cuando existe y el nativo como respaldo.
    /// </summary>
    public static class WebView2Availability
    {
        private static bool? _cached;

        /// <summary>True si hay un runtime de WebView2 utilizable (con caché).</summary>
        public static bool IsAvailable
        {
            get
            {
                if (_cached == null)
                {
                    try
                    {
                        _cached = !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("WebView2 no disponible: " + ex.Message);
                        _cached = false;
                    }
                }
                return _cached.Value;
            }
        }

        /// <summary>Versión del runtime detectado (vacía si no hay).</summary>
        public static string RuntimeVersion
        {
            get
            {
                try { return CoreWebView2Environment.GetAvailableBrowserVersionString() ?? ""; }
                catch { return ""; }
            }
        }
    }
}
