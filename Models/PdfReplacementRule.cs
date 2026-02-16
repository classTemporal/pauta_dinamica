using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Linq;

namespace PautaDinamicaApp.Models
{
    public class PdfReplacementRule : INotifyPropertyChanged
    {
        private string _targetValue = "";
        private string _replacementValue = "";
        private string _textColor = "#000000"; // Color por defecto (Negro)

        // --- Nuevas propiedades para Multi-Campo y Multi-Selección ---

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsValid => true;

        private bool _isSelected;
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

        private ObservableCollection<string> _targetFieldIds = new();
        public ObservableCollection<string> TargetFieldIds
        {
            get => _targetFieldIds;
            set
            {
                if (_targetFieldIds != value)
                {
                    _targetFieldIds = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FieldId)); // Notificar al proxy
                }
            }
        }

        // Proxy para compatibilidad o binding simple
        [System.Text.Json.Serialization.JsonIgnore]
        public string FieldId
        {
            get => _targetFieldIds.FirstOrDefault() ?? "";
            set
            {
                if (!string.IsNullOrEmpty(value) && (!_targetFieldIds.Contains(value) || _targetFieldIds.Count > 1))
                {
                    _targetFieldIds.Clear();
                    _targetFieldIds.Add(value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TargetFieldIds));
                }
            }
        }

        public string TargetValue
        {
            get => _targetValue;
            set
            {
                if (_targetValue != value)
                {
                    _targetValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ReplacementValue
        {
            get => _replacementValue;
            set
            {
                if (_replacementValue != value)
                {
                    _replacementValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TextColor
        {
            get => _textColor;
            set
            {
                if (_textColor != value)
                {
                    _textColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
