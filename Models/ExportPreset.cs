using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PautaDinamicaApp.Models
{
    public class ExportPreset : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = "Nueva Configuración";
        private List<ExportColumnConfig> _columns = new();

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public List<ExportColumnConfig> Columns
        {
            get => _columns;
            set => SetProperty(ref _columns, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
