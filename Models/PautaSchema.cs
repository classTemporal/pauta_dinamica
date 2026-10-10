using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PautaDinamicaApp.Models
{
    public class PautaSchema : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = "Nueva Pauta";
        private DateTime _createdAt = DateTime.Now;
        private bool _isSelected;
        private bool _isRenaming;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsRenaming
        {
            get => _isRenaming;
            set => SetProperty(ref _isRenaming, value);
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsDragging
        {
            get => _isDragging;
            set => SetProperty(ref _isDragging, value);
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsValid => true;

        // --- Categorías de Plantillas ---
        private List<string> _templateCategories = new();

        [System.Text.Json.Serialization.JsonIgnore]
        public List<string> TemplateCategories
        {
            get => _templateCategories;
            set => SetProperty(ref _templateCategories, value);
        }

        // --- Configuración de Correo por Pauta ---
        private EmailMethod _emailMethod = EmailMethod.Mailto;
        private string _emailToTemplate = "";
        private string _emailToHtmlTemplate = "";
        private string _emailCcTemplate = "";
        private string _emailCcHtmlTemplate = "";
        private string _emailSubjectTemplate = "";
        private string _emailSubjectHtmlTemplate = "";
        private string _emailBodyTemplate = "";
        private string _emailBodyHtmlTemplate = "";
        private bool _useAutomatedRecipient = false;
        private bool _isDragging;
        private bool _isDropTarget;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsDropTarget
        {
            get => _isDropTarget;
            set => SetProperty(ref _isDropTarget, value);
        }

        // --- Lógica de Exclusión ---
        private string _excludeByFieldId = ""; // ID del campo a evaluar
        private string _excludeByFieldValue = ""; // Valor que causa exclusión (ej: "100%")

        private string _emailNameFieldId = "";
        private System.Collections.Generic.List<RecipientContact> _recipientContacts = new();

        public EmailMethod EmailMethod { get => _emailMethod; set => SetProperty(ref _emailMethod, value); }
        public string EmailToTemplate { get => _emailToTemplate; set => SetProperty(ref _emailToTemplate, value); }
        public string EmailCcTemplate { get => _emailCcTemplate; set => SetProperty(ref _emailCcTemplate, value); }
        public string EmailSubjectTemplate { get => _emailSubjectTemplate; set => SetProperty(ref _emailSubjectTemplate, value); }
        public string EmailBodyTemplate { get => _emailBodyTemplate; set => SetProperty(ref _emailBodyTemplate, value); }

        /// <summary>
        /// Cuerpo del correo en formato HTML enriquecido (negritas, tablas, imágenes inline).
        /// Solo se usa cuando <see cref="EmailMethod"/> es <see cref="EmailMethod.Outlook"/>,
        /// que es el único capaz de interpretarlo (.HTMLBody).
        ///
        /// Es un campo INDEPENDIENTE de <see cref="EmailBodyTemplate"/> a propósito:
        ///   - Mailto  -> usa EmailBodyTemplate  (texto plano, no puede transportar marcado).
        ///   - Outlook -> usa EmailBodyHtmlTemplate (HTML, admite formato y tablas).
        /// Al estar separados, mailto nunca puede recibir HTML por error.
        /// </summary>
        public string EmailBodyHtmlTemplate { get => _emailBodyHtmlTemplate; set => SetProperty(ref _emailBodyHtmlTemplate, value); }

        // --- Fechas Dinámicas (opcionales) ---
        // Permiten títulos como "Auditorías de llamadas del 03/10/2026 al 09/10/2026"
        // calculando el rango con la fecha del sistema, sin escribirlo a mano.
        private bool _useDynamicDates = false;
        private DynamicDateConfig _dynamicDates = new();

        /// <summary>
        /// Activa la resolución de los tokens de fecha dinámica ([Hoy], [Semana], [Mes],
        /// [Año], [Rango]) en las plantillas de esta pauta.
        /// Por defecto está desactivado, de modo que el comportamiento existente no cambia.
        /// </summary>
        public bool UseDynamicDates { get => _useDynamicDates; set => SetProperty(ref _useDynamicDates, value); }

        /// <summary>Configuración del rango custom usado por el token [Rango].</summary>
        public DynamicDateConfig DynamicDates { get => _dynamicDates; set => SetProperty(ref _dynamicDates, value); }
        public bool UseAutomatedRecipient { get => _useAutomatedRecipient; set => SetProperty(ref _useAutomatedRecipient, value); }
        public string EmailNameFieldId { get => _emailNameFieldId; set => SetProperty(ref _emailNameFieldId, value); }
        public System.Collections.Generic.List<RecipientContact> RecipientContacts { get => _recipientContacts; set => SetProperty(ref _recipientContacts, value); }

        public string ExcludeByFieldId { get => _excludeByFieldId; set => SetProperty(ref _excludeByFieldId, value); }
        public string ExcludeByFieldValue { get => _excludeByFieldValue; set => SetProperty(ref _excludeByFieldValue, value); }

        private string _pdfFileNameFieldId1 = "";
        private string _pdfFileNameFieldId2 = "";
        public string PdfFileNameFieldId1 { get => _pdfFileNameFieldId1; set => SetProperty(ref _pdfFileNameFieldId1, value); }
        public string PdfFileNameFieldId2 { get => _pdfFileNameFieldId2; set => SetProperty(ref _pdfFileNameFieldId2, value); }

        private System.Collections.Generic.List<ExportColumnConfig> _exportConfig = new();
        public System.Collections.Generic.List<ExportColumnConfig> ExportConfig { get => _exportConfig; set => SetProperty(ref _exportConfig, value); }

        private System.Collections.Generic.List<ExportPreset> _exportPresets = new();
        public System.Collections.Generic.List<ExportPreset> ExportPresets { get => _exportPresets; set => SetProperty(ref _exportPresets, value); }

        private System.Collections.Generic.List<ExportColumnConfig> _pdfConfig = new();
        public System.Collections.Generic.List<ExportColumnConfig> PdfConfig { get => _pdfConfig; set => SetProperty(ref _pdfConfig, value); }

        private string _helpContent = "";
        public string HelpContent { get => _helpContent; set => SetProperty(ref _helpContent, value); }

        // --- Contadores Rápidos de Estadísticas ---
        private string _counterField1 = "";
        private string _counterValue1 = "";
        private string _counterField2 = "";
        private string _counterValue2 = "";
        private string _counterField3 = "";
        private string _counterValue3 = "";

        public string CounterField1 { get => _counterField1; set => SetProperty(ref _counterField1, value); }
        public string CounterValue1 { get => _counterValue1; set => SetProperty(ref _counterValue1, value); }
        public string CounterField2 { get => _counterField2; set => SetProperty(ref _counterField2, value); }
        public string CounterValue2 { get => _counterValue2; set => SetProperty(ref _counterValue2, value); }
        public string CounterField3 { get => _counterField3; set => SetProperty(ref _counterField3, value); }
        public string CounterValue3 { get => _counterValue3; set => SetProperty(ref _counterValue3, value); }

        // --- Resaltado de Filas por Pauta ---
        private string _coloringField = "";
        private string _coloringValue = "";
        private string _coloringColor = "#28a745";

        public string ColoringField { get => _coloringField; set => SetProperty(ref _coloringField, value); }
        public string ColoringValue { get => _coloringValue; set => SetProperty(ref _coloringValue, value); }
        public string ColoringColor { get => _coloringColor; set => SetProperty(ref _coloringColor, value); }

        // --- Reglas de Reemplazo para Correos ---
        private System.Collections.ObjectModel.ObservableCollection<EmailReplacementRule> _emailReplacementRules = new();
        public System.Collections.ObjectModel.ObservableCollection<EmailReplacementRule> EmailReplacementRules { get => _emailReplacementRules; set => SetProperty(ref _emailReplacementRules, value); }

        // --- Correos Adicionales Condicionales ---
        // Reglas que disparan un correo extra cuando un campo contiene un valor específico
        // (ej: Calificación = "0%" -> correo de "Detractor alto riesgo").
        private System.Collections.ObjectModel.ObservableCollection<ConditionalEmailRule> _conditionalEmailRules = new();
        public System.Collections.ObjectModel.ObservableCollection<ConditionalEmailRule> ConditionalEmailRules { get => _conditionalEmailRules; set => SetProperty(ref _conditionalEmailRules, value); }

        // --- Adjuntos del Correo Principal ---
        // Qué archivos acompañan el correo principal. Cada correo adicional tiene
        // su propia configuración en la regla. Solo aplica al método Outlook
        // (mailto no admite adjuntos).
        private bool _attachPdfToEmail = true;

        /// <summary>
        /// Incluir el PDF del reporte en los correos. Por defecto <c>true</c>
        /// (comportamiento histórico: el PDF siempre se mandaba).
        /// </summary>
        public bool AttachPdfToEmail { get => _attachPdfToEmail; set => SetProperty(ref _attachPdfToEmail, value); }

        private System.Collections.Generic.List<string> _excludedAttachmentFieldIds = new();

        /// <summary>
        /// IDs de campos de archivo adjunto que NO se enviarán con el correo.
        /// Es una capa adicional a <c>FieldDefinition.AttachToEmail</c>: un campo solo se
        /// adjunta si su propio flag lo permite Y su ID no está en esta lista.
        /// Vacío = se envían todos (comportamiento histórico).
        /// </summary>
        public System.Collections.Generic.List<string> ExcludedAttachmentFieldIds { get => _excludedAttachmentFieldIds; set => SetProperty(ref _excludedAttachmentFieldIds, value); }

        // --- Reglas de Reemplazo para PDF ---
        private System.Collections.ObjectModel.ObservableCollection<PdfReplacementRule> _pdfReplacementRules = new();
        public System.Collections.ObjectModel.ObservableCollection<PdfReplacementRule> PdfReplacementRules { get => _pdfReplacementRules; set => SetProperty(ref _pdfReplacementRules, value); }

        // --- Orden de Campos en Dashboard (Card 39) ---
        // Lista de IDs de campos que define el orden de visualización en el dashboard principal.
        // Si está vacío, se usa el orden natural de la configuración.
        private List<string> _dashboardFieldOrder = new();
        public List<string> DashboardFieldOrder
        {
            get => _dashboardFieldOrder;
            set => SetProperty(ref _dashboardFieldOrder, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}