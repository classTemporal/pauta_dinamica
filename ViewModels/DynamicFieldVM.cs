using System;
using System.Collections.Generic;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.ViewModels
{
    public class DynamicFieldVM : ViewModelBase
    {
        private object? _value;
        private string? _validationError;
        private bool _isValid = true; // Backing field for IsValid

        public FieldDefinition Definition { get; }

        public DynamicFieldVM(FieldDefinition definition)
        {
            Definition = definition;

            // Default values based on type
            if (definition.Type == FieldType.Boolean) _value = false;
            if (definition.Type == FieldType.Date) _value = DateTime.Now;

            // We don't call Validate() here so the form starts "clean" (no red borders)
        }

        public string Id => Definition.Id;
        public string Label => Definition.Label;
        public string Category => Definition.Category;
        public FieldType Type => Definition.Type;
        public bool IsRequired => Definition.IsRequired;
        public List<string> Options => Definition.Options;

        public object? Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    Validate();
                }
            }
        }

        public string? ValidationError
        {
            get => _validationError;
            set => SetProperty(ref _validationError, value);
        }

        // IsValid is now a settable property
        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        public void Reset()
        {
            // Reset value to default
            if (Definition.Type == FieldType.Boolean) Value = false;
            else if (Definition.Type == FieldType.Date) Value = DateTime.Now;
            else Value = null;

            // Clear validation state
            IsValid = true;
            ValidationError = "";
            OnPropertyChanged(nameof(Value)); // Notify that Value might have changed
        }

        public bool Validate()
        {
            if (IsRequired && (Value == null || string.IsNullOrWhiteSpace(Value.ToString())))
            {
                IsValid = false;
                ValidationError = "Este campo es obligatorio.";
                return false;
            }

            IsValid = true;
            ValidationError = "";
            return true;
        }
    }
}
