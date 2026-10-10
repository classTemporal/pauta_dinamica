using System.Threading.Tasks;

namespace PautaDinamicaApp.Views.HtmlEditor
{
    /// <summary>
    /// Contrato común de los editores de cuerpo HTML (WebView2 y nativo).
    /// Permite que <c>SettingsWindow</c> trabaje con cualquiera de los dos
    /// sin saber cuál está activo.
    /// </summary>
    public interface IHtmlEditor
    {
        /// <summary>HTML del cuerpo (two-way hacia la plantilla).</summary>
        string Html { get; set; }

        /// <summary>Visibilidad del control (heredada de UserControl).</summary>
        System.Windows.Visibility Visibility { get; }

        /// <summary>Vuelca el contenido actual a <see cref="Html"/>.</summary>
        Task FlushAsync();

        /// <summary>Inserta texto plano ([Etiqueta]/[Fecha]) en el cursor.</summary>
        Task InsertTextAtCaretAsync(string text);
    }
}
