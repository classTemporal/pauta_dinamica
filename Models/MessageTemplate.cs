using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace PautaDinamicaApp.Models
{
    public class MessageTemplate : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _content = string.Empty;
        private string _pautaId = string.Empty;
        private string _category = string.Empty;
        private bool _isSelected;

        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public string Content { get => _content; set { _content = value; OnPropertyChanged(); } }
        public string PautaId { get => _pautaId; set { _pautaId = value; OnPropertyChanged(); } }
        public string Category { get => _category; set { _category = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
