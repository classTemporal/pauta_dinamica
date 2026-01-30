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
        public bool IsValid => true;

        // --- Configuración de Correo por Pauta ---
        private EmailMethod _emailMethod = EmailMethod.Mailto;
        private string _emailToTemplate = "";
        private string _emailCcTemplate = "";
        private string _emailSubjectTemplate = "";
        private string _emailBodyTemplate = "";
        private bool _useAutomatedRecipient = false;

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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
