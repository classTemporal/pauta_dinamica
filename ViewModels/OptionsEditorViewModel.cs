using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Linq;

namespace PautaDinamicaApp.ViewModels
{
    public class OptionsEditorViewModel : ViewModelBase
    {
        private ObservableCollection<string> _options;
        private string _newOptionText = string.Empty;

        public OptionsEditorViewModel(System.Collections.Generic.List<string> existingOptions)
        {
            _options = new ObservableCollection<string>(existingOptions ?? new System.Collections.Generic.List<string>());

            AddOptionCommand = new RelayCommand(_ => AddOption(), _ => !string.IsNullOrWhiteSpace(NewOptionText));
            RemoveOptionCommand = new RelayCommand(p => RemoveOption(p as string));
        }

        public ObservableCollection<string> Options
        {
            get => _options;
            set => SetProperty(ref _options, value);
        }

        public string NewOptionText
        {
            get => _newOptionText;
            set => SetProperty(ref _newOptionText, value);
        }

        public ICommand AddOptionCommand { get; }
        public ICommand RemoveOptionCommand { get; }

        private void AddOption()
        {
            if (string.IsNullOrWhiteSpace(NewOptionText)) return;

            string cleaned = NewOptionText.Trim();
            if (Options.Any(o => o.Equals(cleaned, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Esta opción ya existe en la lista.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Options.Add(cleaned);
            NewOptionText = string.Empty;
        }

        private void RemoveOption(string? option)
        {
            if (option == null) return;

            var result = MessageBox.Show($"¿Realmente desea eliminar la opción \"{option}\"?",
                                       "Confirmar eliminación",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Options.Remove(option);
            }
        }
    }
}
