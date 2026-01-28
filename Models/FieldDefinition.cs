using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace PautaDinamicaApp.Models
{
    public enum FieldType
    {
        Text,
        Numeric,
        Date,
        Boolean,
        Dropdown,
        Separator
    }

    public class FieldDefinition : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Category { get; set; } = "General"; // To group fields like in the image
        public int Order { get; set; } // To maintain layout order
        public FieldType Type { get; set; } = FieldType.Text;
        public bool IsRequired { get; set; }
        public List<string> Options { get; set; } = new List<string>();

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _isSelected;

        [System.Text.Json.Serialization.JsonIgnore]
        public string OptionsString
        {
            get => string.Join(", ", Options);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Options = new List<string>();
                }
                else
                {
                    Options = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(s => s.Trim())
                                   .ToList();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void EnsureDefaultOptions()
        {
            if (Type == FieldType.Dropdown && (Options == null || Options.Count == 0))
            {
                Options = new List<string> { "Cumple", "No Cumple", "N/A" };
            }
        }
    }
}
