using System;
using System.Collections.Generic;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;
using System.Windows.Input;
using System.Linq;
using System.Windows;

namespace PautaDinamicaApp.ViewModels
{
    public class DynamicFieldVM : ViewModelBase
    {
        private object? _value;
        private string? _validationError;
        private bool _isValid = true;

        public FieldDefinition Definition { get; }

        public DynamicFieldVM(FieldDefinition definition)
        {
            Definition = definition;
            InitializeDefaultValue();
            PickTimeCommand = new RelayCommand(_ => PickTime());
            PickDateCommand = new RelayCommand(_ => PickDate());
            OpenTemplatesCommand = new RelayCommand(_ => OpenTemplatePicker());
        }

        public ICommand PickTimeCommand { get; }
        public ICommand PickDateCommand { get; }
        public ICommand OpenTemplatesCommand { get; }

        private void PickTime()
        {
            if (Definition.Type != FieldType.Time) return;

            string currentVal = Value?.ToString() ?? "";
            var selector = new PautaDinamicaApp.Views.TimeSelectorWindow(currentVal, Definition.TimeFormat ?? "HH:mm");
            selector.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            if (selector.ShowDialog() == true)
            {
                string val = selector.SelectedValue;
                if (val == "NOW")
                {
                    val = DateTime.Now.ToString(Definition.TimeFormat ?? "HH:mm");
                }
                Value = val;
            }
        }

        private void PickDate()
        {
            if (Definition.Type != FieldType.Date) return;

            string currentVal = Value?.ToString() ?? "";
            var selector = new PautaDinamicaApp.Views.DateSelectorWindow(currentVal);
            selector.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            if (selector.ShowDialog() == true)
            {
                string val = selector.SelectedValue;
                if (val == "TODAY")
                {
                    val = DateTime.Now.ToString("dd/MM/yyyy");
                }
                Value = val;
            }
        }

        private void OpenTemplatePicker()
        {
            var storage = new StorageService();
            var templates = storage.LoadTemplates();
            if (!templates.Any())
            {
                System.Windows.MessageBox.Show("No hay plantillas disponibles. Puedes agregarlas en Configuración General > Plantillas.", "Gestor de Plantillas", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new PautaDinamicaApp.Views.TemplatePickerWindow(templates);
            win.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            if (win.ShowDialog() == true)
            {
                string selectedContent = win.SelectedTemplateContent;
                string currentText = Value?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(currentText))
                {
                    Value = selectedContent;
                }
                else
                {
                    string separator = Type == FieldType.TextArea ? (currentText.EndsWith("\n") ? "" : "\n\n") : " ";
                    Value = currentText + separator + selectedContent;
                }
            }
        }

        private void InitializeDefaultValue()
        {
            if (!string.IsNullOrEmpty(Definition.DefaultValue))
            {
                if (Definition.Type == FieldType.Boolean)
                {
                    if (Definition.DefaultValue == "1") _value = true;
                    else if (Definition.DefaultValue == "0") _value = false;
                    else if (bool.TryParse(Definition.DefaultValue, out bool b)) _value = b;
                }
                else if (Definition.Type == FieldType.Date)
                {
                    if (DateTime.TryParse(Definition.DefaultValue, out DateTime d)) _value = d.ToString("dd/MM/yyyy");
                    else if (Definition.DefaultValue.Equals("TODAY", StringComparison.OrdinalIgnoreCase)) _value = DateTime.Now.ToString("dd/MM/yyyy");
                    else _value = Definition.DefaultValue;
                }
                else if (Definition.Type == FieldType.Time)
                {
                    string format = Definition.TimeFormat ?? "HH:mm";
                    if (DateTime.TryParse(Definition.DefaultValue, out DateTime d)) _value = d.ToString(format);
                    else if (Definition.DefaultValue.Equals("NOW", StringComparison.OrdinalIgnoreCase)) _value = DateTime.Now.ToString(format);
                    else _value = Definition.DefaultValue;
                }
                else
                {
                    _value = Definition.DefaultValue;
                }
            }
            else
            {
                if (Definition.Type == FieldType.Boolean) _value = false;
                else _value = null;
            }
        }

        public string Id => Definition.Id;
        public string Label => Definition.Label;
        public string Category => Definition.Category;
        public FieldType Type => Definition.Type;
        public bool IsRequired => Definition.IsRequired;
        public List<string> Options => Definition.Options;
        public int MaxLength => Definition.MaxLength;
        public int CurrentLength => _value?.ToString()?.Length ?? 0;
        public bool ShowTemplateButton => Type == FieldType.Text || Type == FieldType.TextArea;

        public object? Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    OnPropertyChanged(nameof(CurrentLength));
                    Validate();
                }
            }
        }

        public string? ValidationError
        {
            get => _validationError;
            set => SetProperty(ref _validationError, value);
        }

        public override bool IsValid
        {
            get => _isValid;
            protected set
            {
                if (_isValid != value)
                {
                    _isValid = value;
                    OnPropertyChanged();
                }
            }
        }

        public void Reset()
        {
            if (Definition.KeepValueOnReset) { }
            else InitializeDefaultValue();

            IsValid = true;
            ValidationError = "";
            OnPropertyChanged(nameof(Value));
        }

        public bool Validate()
        {
            string strValue = Value?.ToString() ?? "";

            if (IsRequired && string.IsNullOrWhiteSpace(strValue))
            {
                IsValid = false;
                ValidationError = "Este campo es obligatorio.";
                return false;
            }

            if (strValue.Length > MaxLength && MaxLength > 0)
            {
                IsValid = false;
                ValidationError = $"El valor no puede exceder los {MaxLength} caracteres.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(strValue))
            {
                if (Type == FieldType.Numeric)
                {
                    if (!double.TryParse(strValue, out _))
                    {
                        IsValid = false;
                        ValidationError = "Debe ser un número válido.";
                        return false;
                    }
                }
                else if (Type == FieldType.Date)
                {
                    if (!DateTime.TryParseExact(strValue, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out _))
                    {
                        if (!DateTime.TryParse(strValue, out _))
                        {
                            IsValid = false;
                            ValidationError = "Formato inválido (dd/MM/yyyy).";
                            return false;
                        }
                    }
                }
                else if (Type == FieldType.Time)
                {
                    string format = Definition.TimeFormat ?? "HH:mm";
                    string dummyDate = DateTime.Now.ToString("dd/MM/yyyy");
                    string combined = $"{dummyDate} {strValue}";

                    if (!DateTime.TryParseExact(combined, $"dd/MM/yyyy {format}", null, System.Globalization.DateTimeStyles.None, out _))
                    {
                        if (!DateTime.TryParse(strValue, out _))
                        {
                            IsValid = false;
                            ValidationError = $"Formato inválido ({format}).";
                            return false;
                        }
                    }
                }
            }

            IsValid = true;
            ValidationError = "";
            return true;
        }
    }
}
