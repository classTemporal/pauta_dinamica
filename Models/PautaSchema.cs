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

        // --- Configuración de Correo por Pauta ---
        private EmailMethod _emailMethod = EmailMethod.Mailto;
        private string _emailToTemplate = "";
        private string _emailCcTemplate = "";
        private string _emailSubjectTemplate = "";
        private string _emailBodyTemplate = "";
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
