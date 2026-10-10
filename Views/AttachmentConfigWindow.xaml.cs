using System.Collections.Generic;
using System.Linq;
using System.Windows;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views
{
    /// <summary>
    /// Opción de envío para un campo de archivo adjunto en el diálogo de adjuntos.
    /// <c>IsIncluded</c> refleja la lista del correo (no el flag del campo):
    /// la casilla solo manda si además el campo permite adjuntar.
    /// </summary>
    public class AttachmentFieldOption
    {
        public string FieldId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        /// <summary>El campo tiene activo "Adjuntar automáticamente" en sus opciones.</summary>
        public bool FieldAllows { get; set; } = true;

        /// <summary>Marcado en este diálogo (no está en la lista de excluidos).</summary>
        public bool IsIncluded { get; set; } = true;
    }

    /// <summary>
    /// Diálogo "Configurar adjuntos": elige qué archivos acompañan UN correo concreto
    /// (PDF del reporte + campos de archivo adjunto, tipo check).
    /// Es reutilizable: recibe los valores actuales y el nombre del correo, y al aceptar
    /// expone el resultado en <c>ResultIncludePdf</c> / <c>ResultExcludedIds</c> para que
    /// el llamador lo guarde donde corresponda (correo principal o regla adicional).
    /// No escribe nada por sí mismo.
    /// </summary>
    public partial class AttachmentConfigWindow : Window
    {
        private readonly List<AttachmentFieldOption> _options;
        private readonly HashSet<string> _initialExcluded;

        /// <summary>Resultado: incluir el PDF (válido solo si se aceptó).</summary>
        public bool ResultIncludePdf { get; private set; } = true;

        /// <summary>Resultado: IDs excluidos (válido solo si se aceptó).</summary>
        public List<string> ResultExcludedIds { get; private set; } = new();

        public AttachmentConfigWindow(bool includePdf, IEnumerable<string>? excludedIds, IEnumerable<FieldDefinition> fields, string emailName = "este correo")
        {
            InitializeComponent();

            ResultIncludePdf = includePdf;
            PdfCheck.IsChecked = includePdf;

            _initialExcluded = new HashSet<string>(excludedIds ?? Enumerable.Empty<string>());
            _options = (fields ?? Enumerable.Empty<FieldDefinition>())
                .Where(f => f.Type == FieldType.FileAttachment)
                .Select(f => new AttachmentFieldOption
                {
                    FieldId = f.Id,
                    Label = string.IsNullOrWhiteSpace(f.Label) ? "(Campo sin etiqueta)" : f.Label.Trim(),
                    FieldAllows = f.AttachToEmail,
                    IsIncluded = !_initialExcluded.Contains(f.Id)
                })
                .ToList();

            FieldsList.ItemsSource = _options;

            Title = $"Configurar Adjuntos — {emailName}";
            DescriptionText.Text = $"Marca los archivos que se enviarán con {emailName}. Solo aplica al método Outlook (mailto no admite adjuntos).";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var excluded = new List<string>();

            foreach (var o in _options)
            {
                if (!o.FieldAllows)
                {
                    // Deshabilitado en el campo: no se tocó aquí, se conserva su estado previo.
                    if (_initialExcluded.Contains(o.FieldId)) excluded.Add(o.FieldId);
                }
                else if (!o.IsIncluded)
                {
                    excluded.Add(o.FieldId);
                }
            }

            ResultIncludePdf = PdfCheck.IsChecked == true;
            ResultExcludedIds = excluded;

            DialogResult = true;
        }
    }
}
