using System.Collections.Generic;
using System.Linq;
using System.Windows;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views
{
    /// <summary>
    /// Opción de envío para un campo de archivo adjunto en el diálogo de adjuntos.
    /// <c>IsIncluded</c> refleja la lista de la pauta (no el flag del campo):
    /// la casilla solo manda si además el campo permite adjuntar.
    /// </summary>
    public class AttachmentFieldOption
    {
        public string FieldId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        /// <summary>El campo tiene activo "Adjuntar automáticamente" en sus opciones.</summary>
        public bool FieldAllows { get; set; } = true;

        /// <summary>Marcado en este diálogo (no está en ExcludedAttachmentFieldIds).</summary>
        public bool IsIncluded { get; set; } = true;
    }

    /// <summary>
    /// Diálogo "Configurar adjuntos": elige qué archivos acompañan el correo principal
    /// y los adicionales (PDF del reporte + campos de archivo adjunto, tipo check).
    /// Solo escribe en la pauta (<c>AttachPdfToEmail</c> / <c>ExcludedAttachmentFieldIds</c>),
    /// que es lo que persiste la ventana de configuración; no toca los campos.
    /// </summary>
    public partial class AttachmentConfigWindow : Window
    {
        private readonly PautaSchema _pauta;
        private readonly List<AttachmentFieldOption> _options;

        public AttachmentConfigWindow(PautaSchema pauta, IEnumerable<FieldDefinition> fields)
        {
            InitializeComponent();
            _pauta = pauta ?? throw new System.ArgumentNullException(nameof(pauta));

            PdfCheck.IsChecked = pauta.AttachPdfToEmail;

            var excluded = new HashSet<string>(pauta.ExcludedAttachmentFieldIds ?? new List<string>());
            _options = (fields ?? Enumerable.Empty<FieldDefinition>())
                .Where(f => f.Type == FieldType.FileAttachment)
                .Select(f => new AttachmentFieldOption
                {
                    FieldId = f.Id,
                    Label = string.IsNullOrWhiteSpace(f.Label) ? "(Campo sin etiqueta)" : f.Label.Trim(),
                    FieldAllows = f.AttachToEmail,
                    IsIncluded = !excluded.Contains(f.Id)
                })
                .ToList();

            FieldsList.ItemsSource = _options;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var oldExcluded = new HashSet<string>(_pauta.ExcludedAttachmentFieldIds ?? new List<string>());
            var excluded = new List<string>();

            foreach (var o in _options)
            {
                if (!o.FieldAllows)
                {
                    // Deshabilitado en el campo: no se tocó aquí, se conserva su estado previo.
                    if (oldExcluded.Contains(o.FieldId)) excluded.Add(o.FieldId);
                }
                else if (!o.IsIncluded)
                {
                    excluded.Add(o.FieldId);
                }
            }

            _pauta.AttachPdfToEmail = PdfCheck.IsChecked == true;
            _pauta.ExcludedAttachmentFieldIds = excluded;

            DialogResult = true;
        }
    }
}
