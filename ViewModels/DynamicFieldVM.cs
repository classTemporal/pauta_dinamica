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
            InitializeDefaultValue();
        }

        private void InitializeDefaultValue()
        {
            if (!string.IsNullOrEmpty(Definition.DefaultValue))
            {
                if (Definition.Type == FieldType.Boolean)
                {
                    if (bool.TryParse(Definition.DefaultValue, out bool b)) _value = b;
                }
                else if (Definition.Type == FieldType.Date)
                {
                    if (DateTime.TryParse(Definition.DefaultValue, out DateTime d)) _value = d;
                    else if (Definition.DefaultValue.Equals("TODAY", StringComparison.OrdinalIgnoreCase)) _value = DateTime.Now;
                }
                else
                {
                    _value = Definition.DefaultValue;
                }
            }
            else
            {
                // Fallbacks if no default is specified
                if (Definition.Type == FieldType.Boolean) _value = false;
                else if (Definition.Type == FieldType.Date) _value = DateTime.Now;
                else _value = null;
            }
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
            if (Definition.KeepValueOnReset)
            {
                // No tocamos Value, mantenemos lo que tenga
            }
            else
            {
                InitializeDefaultValue();
            }

            // Clear validation state
            IsValid = true;
            ValidationError = "";
            OnPropertyChanged(nameof(Value));
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
