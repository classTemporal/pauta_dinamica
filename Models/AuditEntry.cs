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
