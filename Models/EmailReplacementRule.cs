using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Linq;

namespace PautaDinamicaApp.Models
{
    public class EmailReplacementRule : INotifyPropertyChanged
    {
        private string _targetValue = "";
        private string _replacementValue = "";

        // --- Nuevas propiedades para Multi-Campo y Multi-Selección ---

        // Fix binding warning
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

        // Proxy para compatibilidad o binding simple (obtiene/establece el primero)
        // Esto permite que el código existente que usa FieldId siga funcionando
        // pero lo mapeamos a la lista interna.
        [System.Text.Json.Serialization.JsonIgnore]
        public string FieldId
        {
            get => _targetFieldIds.FirstOrDefault() ?? "";
            set
            {
                // Si cambiamos FieldId (desde UI antigua), reseteamos la lista a solo este elemento
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
