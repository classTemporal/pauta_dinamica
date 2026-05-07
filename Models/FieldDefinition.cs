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
        Time,
        Boolean,
        Dropdown,
        Separator,
        Calculation,
        Average,
        TextArea
    }

    public enum CalculationRounding
    {
        None,
        Up,
        Down
    }

    public class ScoringRule
    {
        public string FieldId { get; set; } = string.Empty;
        public List<ValueScoreMapping> Mappings { get; set; } = new();
        public double Weight { get; set; } = 1.0;
        public string NaValue { get; set; } = "N/A";
    }

    public class ValueScoreMapping
    {
        public string Value { get; set; } = string.Empty;
        public double Score { get; set; } = 1.0;
    }

    public class AutoSelectRule
    {
        public string SourceFieldId { get; set; } = string.Empty;
        public string Operator { get; set; } = "="; // =, >, <, >=, <=
        public string Value { get; set; } = string.Empty;
        public string TargetValue { get; set; } = string.Empty; // Valor de la lista a seleccionar
    }

    public class FieldDefinition : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _label = string.Empty;
        private string _category = "General";
        private int _order;
        private FieldType _type = FieldType.Text;
        private bool _isRequired;
        private List<string> _options = new();
        private List<ScoringRule> _scoringRules = new();
        private List<string> _targetIds = new();
        private bool _useCustomWeights;
        private string _defaultValue = string.Empty;
        private bool _keepValueOnReset;
        private string _timeFormat = "HH:mm";
        private string _zeroTriggerFieldId = string.Empty;
        private List<string> _zeroTriggerFieldIds = new();
        private string _zeroTriggerValue = string.Empty;
        private bool _enableZeroTrigger;
        private bool _showDecimals = true;
        private CalculationRounding _rounding = CalculationRounding.None;
        private List<AutoSelectRule> _autoSelectRules = new();

        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public string Label { get => _label; set { _label = value; OnPropertyChanged(); } }
        public string Category { get => _category; set { _category = value; OnPropertyChanged(); } }
        public int Order { get => _order; set { _order = value; OnPropertyChanged(); } }
        public FieldType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    ApplyTypeDefaults();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DefaultValue)); // Trigger UI refresh for predeterminado
                }
            }
        }

        private void ApplyTypeDefaults()
        {
            switch (Type)
            {
                case FieldType.TextArea: MaxLength = 1000; break;
                case FieldType.Text: MaxLength = 255; break;
                case FieldType.Numeric: MaxLength = 15; break;
                case FieldType.Date: MaxLength = 10; break;
                case FieldType.Time: MaxLength = 10; break;
            }
        }
        public bool IsRequired { get => _isRequired; set { _isRequired = value; OnPropertyChanged(); } }
        public List<string> Options { get => _options; set { _options = value; OnPropertyChanged(); } }
        public string DefaultValue { get => _defaultValue; set { _defaultValue = value; OnPropertyChanged(); } }
        public string TimeFormat { get => _timeFormat; set { _timeFormat = value; OnPropertyChanged(); } }
        public bool KeepValueOnReset { get => _keepValueOnReset; set { _keepValueOnReset = value; OnPropertyChanged(); } }
        public string ZeroTriggerFieldId { get => _zeroTriggerFieldId; set { _zeroTriggerFieldId = value; OnPropertyChanged(); } }
        public List<string> ZeroTriggerFieldIds { get => _zeroTriggerFieldIds; set { _zeroTriggerFieldIds = value ?? new(); OnPropertyChanged(); } }
        public string ZeroTriggerValue { get => _zeroTriggerValue; set { _zeroTriggerValue = value; OnPropertyChanged(); } }
        public bool EnableZeroTrigger { get => _enableZeroTrigger; set { _enableZeroTrigger = value; OnPropertyChanged(); } }
        public bool ShowDecimals { get => _showDecimals; set { _showDecimals = value; OnPropertyChanged(); } }
        public CalculationRounding Rounding { get => _rounding; set { _rounding = value; OnPropertyChanged(); } }
        private int _maxLength = 255;
        public int MaxLength { get => _maxLength; set { _maxLength = value; OnPropertyChanged(); } }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsValid => true; // Dummy property to satisfy global styles in ConfigWindow

        // --- LÓGICA DE CÁLCULO ---
        public List<ScoringRule> ScoringRules { get => _scoringRules; set { _scoringRules = value; OnPropertyChanged(); } }
        public List<AutoSelectRule> AutoSelectRules { get => _autoSelectRules; set { _autoSelectRules = value; OnPropertyChanged(); } }
        public List<string> TargetIds { get => _targetIds; set { _targetIds = value; OnPropertyChanged(); } }
        public bool UseCustomWeights { get => _useCustomWeights; set { _useCustomWeights = value; OnPropertyChanged(); } }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        private bool _isSelected;

        // Propiedad para activar la advertencia de duplicados
        private bool _warnOnDuplicate = false;
        public bool WarnOnDuplicate
        {
            get => _warnOnDuplicate;
            set { _warnOnDuplicate = value; OnPropertyChanged(); }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public string OptionsString
        {
            get => string.Join(", ", Options);
            set
            {
                Options = (value ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim()).ToList();
                OnPropertyChanged();
            }
        }

        public FieldDefinition Clone()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            return System.Text.Json.JsonSerializer.Deserialize<FieldDefinition>(json)!;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void EnsureDefaultOptions()
        {
            if (Type == FieldType.Dropdown && (Options == null || Options.Count == 0))
                Options = new List<string> { "Cumple", "No Cumple", "N/A" };

            // Migración para campos antiguos sin MaxLength (o puestos a 0 por error)
            if (MaxLength <= 0)
            {
                ApplyTypeDefaults();
            }
        }
    }
}
