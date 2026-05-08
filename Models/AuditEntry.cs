using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PautaDinamicaApp.Models
{
    public class AuditEntry : INotifyPropertyChanged
    {
        public string RecordId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public double InternalDurationMinutes { get; set; }
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();

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
        public string? RowColor
        {
            get => _rowColor;
            set
            {
                if (_rowColor != value)
                {
                    _rowColor = value;
                    OnPropertyChanged();
                }
            }
        }
        private string? _rowColor;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool HasMissingAttachments
        {
            get => _hasMissingAttachments;
            set
            {
                if (_hasMissingAttachments != value)
                {
                    _hasMissingAttachments = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _hasMissingAttachments;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsValid => true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyUpdate()
        {
            OnPropertyChanged(nameof(Values));
            OnPropertyChanged(nameof(Timestamp));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
