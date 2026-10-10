using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PautaDinamicaApp.Models
{
    public enum ConditionalEmailMode
    {
        Ask,
        Auto
    }

    /// <summary>
    /// Regla de correo adicional disparada cuando un campo de la auditoría
    /// contiene un valor específico (ej: Calificación = "0%").
    /// Ask = preguntar al usuario (agrupado por regla); Auto = abrir directamente.
    /// </summary>
    public class ConditionalEmailRule : INotifyPropertyChanged
    {
        private string _name = "Nuevo correo adicional";
        private string _triggerFieldId = "";
        private string _triggerValue = "";
        private ConditionalEmailMode _mode = ConditionalEmailMode.Ask;
        private string _toTemplate = "";
        private string _ccTemplate = "";
        private string _subjectTemplate = "";
        private string _subjectHtmlTemplate = "";
        private string _bodyTemplate = "";
        private string _bodyHtmlTemplate = "";

        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public string TriggerFieldId { get => _triggerFieldId; set => SetProperty(ref _triggerFieldId, value); }
        public string TriggerValue { get => _triggerValue; set => SetProperty(ref _triggerValue, value); }
        public ConditionalEmailMode Mode
        {
            get => _mode;
            set
            {
                if (Equals(_mode, value)) return;
                _mode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SendWithoutConfirm));
            }
        }

        /// <summary>
        /// Check de UI: marcado = enviar directo sin preguntar (Auto);
        /// desmarcado = pedir confirmación agrupada (Ask).
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool SendWithoutConfirm
        {
            get => _mode == ConditionalEmailMode.Auto;
            set => Mode = value ? ConditionalEmailMode.Auto : ConditionalEmailMode.Ask;
        }

        public string ToTemplate { get => _toTemplate; set => SetProperty(ref _toTemplate, value); }
        public string CcTemplate { get => _ccTemplate; set => SetProperty(ref _ccTemplate, value); }
        public string SubjectTemplate { get => _subjectTemplate; set => SetProperty(ref _subjectTemplate, value); }
        public string BodyTemplate { get => _bodyTemplate; set => SetProperty(ref _bodyTemplate, value); }

        /// <summary>
        /// Cuerpo en HTML enriquecido. Se usa solo cuando la pauta está en método Outlook;
        /// en mailto se usa <see cref="BodyTemplate"/> (texto plano). Ver la nota en
        /// <see cref="PautaSchema.EmailBodyHtmlTemplate"/>.
        /// </summary>
        public string BodyHtmlTemplate { get => _bodyHtmlTemplate; set => SetProperty(ref _bodyHtmlTemplate, value); }

        // --- Adjuntos de ESTE correo adicional (independiente del principal) ---
        private bool _attachPdfToEmail = true;

        /// <summary>
        /// Incluir el PDF del reporte en este correo adicional. Por defecto <c>true</c>.
        /// </summary>
        public bool AttachPdfToEmail
        {
            get => _attachPdfToEmail;
            set
            {
                if (Equals(_attachPdfToEmail, value)) return;
                SetProperty(ref _attachPdfToEmail, value);
                OnPropertyChanged(nameof(AttachmentSummary));
            }
        }

        private System.Collections.Generic.List<string> _excludedAttachmentFieldIds = new();

        /// <summary>
        /// IDs de campos de archivo adjunto que NO se enviarán con este correo adicional.
        /// Vacío = se envían todos (comportamiento histórico).
        /// </summary>
        public System.Collections.Generic.List<string> ExcludedAttachmentFieldIds
        {
            get => _excludedAttachmentFieldIds;
            set
            {
                if (Equals(_excludedAttachmentFieldIds, value)) return;
                SetProperty(ref _excludedAttachmentFieldIds, value);
                OnPropertyChanged(nameof(AttachmentSummary));
            }
        }

        /// <summary>
        /// Resumen corto para mostrar junto al botón de adjuntos de la regla.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string AttachmentSummary
        {
            get
            {
                string pdf = _attachPdfToEmail ? "PDF: sí" : "PDF: no";
                int n = _excludedAttachmentFieldIds?.Count ?? 0;
                return n == 0 ? $"{pdf} · Todos los adjuntos" : $"{pdf} · {n} excluido(s)";
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsValid => true;

        private bool _isSelected;
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// Determina si la regla aplica a un registro (igualdad exacta, sin distinción de mayúsculas).
        /// </summary>
        public bool Matches(System.Collections.Generic.IDictionary<string, object> values)
        {
            if (string.IsNullOrWhiteSpace(TriggerFieldId) || string.IsNullOrWhiteSpace(TriggerValue))
                return false;
            if (values == null || !values.TryGetValue(TriggerFieldId, out var val) || val == null)
                return false;
            return string.Equals(val.ToString()?.Trim() ?? "", TriggerValue.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
