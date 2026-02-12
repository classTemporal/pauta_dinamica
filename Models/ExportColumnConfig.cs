using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PautaDinamicaApp.Models
{
    public class ExportColumnConfig : INotifyPropertyChanged
    {
        private string _fieldId = "";
        private string _originalLabel = "";
        private string _customHeader = "";
        private int _order;
        private bool _isVisible = true;

        public string FieldId
        {
            get => _fieldId;
            set => SetProperty(ref _fieldId, value);
        }

        public string OriginalLabel // Persistent: Used to detect if CustomHeader handles automatic sync with Field Label
        {
            get => _originalLabel;
            set => SetProperty(ref _originalLabel, value);
        }

        public string CustomHeader
        {
            get => _customHeader;
            set => SetProperty(ref _customHeader, value);
        }

        public int Order
        {
            get => _order;
            set => SetProperty(ref _order, value);
        }

        public bool IsVisible // Used for column visibility in UI if implemented, or deprecated by IsExportEnabled
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        private bool _isExportEnabled = true;
        public bool IsExportEnabled
        {
            get => _isExportEnabled;
            set => SetProperty(ref _isExportEnabled, value);
        }

        // Fix binding errors
        public bool IsValid => true;

        private FieldType _type = FieldType.Text;
        public FieldType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        private bool _isSelected;
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
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
