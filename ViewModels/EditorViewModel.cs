using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;
using PautaDinamicaApp.Views;

namespace PautaDinamicaApp.ViewModels
{
    public class EditorViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;
        private ObservableCollection<FieldDefinition> _fields;

        public EditorViewModel()
        {
            _storageService = new StorageService();
            var config = _storageService.LoadConfiguration()
                            .OrderBy(f => f.Order);

            foreach (var field in config)
            {
                field.EnsureDefaultOptions();
            }

            _fields = new ObservableCollection<FieldDefinition>(config);

            AddFieldCommand = new RelayCommand(_ => AddField());
            AddSectionCommand = new RelayCommand(_ => AddSection());
            RemoveFieldCommand = new RelayCommand(p => RemoveField(p as FieldDefinition));
            MoveUpCommand = new RelayCommand(p => MoveUp(p as FieldDefinition));
            MoveDownCommand = new RelayCommand(p => MoveDown(p as FieldDefinition));
            ConfigureOptionsCommand = new RelayCommand(p => ConfigureOptions(p as FieldDefinition));
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());

            // Tipos disponibles para el Combo
            AvailableTypes = Enum.GetValues(typeof(FieldType)).Cast<FieldType>().ToList();
        }

        public bool IsSaveSuccessful { get; private set; }

        public ObservableCollection<FieldDefinition> Fields
        {
            get => _fields;
            set => SetProperty(ref _fields, value);
        }

        public System.Collections.Generic.List<FieldType> AvailableTypes { get; }

        public ICommand AddFieldCommand { get; }
        public ICommand AddSectionCommand { get; }
        public ICommand RemoveFieldCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand ConfigureOptionsCommand { get; }
        public ICommand SaveConfigCommand { get; }

        private void AddField()
        {
            var lastField = Fields.OrderBy(f => f.Order).LastOrDefault();
            Fields.Add(new FieldDefinition
            {
                Id = "f_" + Guid.NewGuid().ToString().Substring(0, 4),
                Label = "Nuevo Campo",
                // El nuevo campo hereda la categoría o sección actual
                Category = lastField?.Category ?? "General",
                Type = FieldType.Text,
                Order = (lastField?.Order ?? 0) + 1
            });
        }

        private void AddSection()
        {
            var lastField = Fields.OrderBy(f => f.Order).LastOrDefault();
            Fields.Add(new FieldDefinition
            {
                Id = "s_" + Guid.NewGuid().ToString().Substring(0, 4),
                Label = "Nuevo Cuadro",
                Category = "--- SECCIÓN ---", // Marcador visual interno
                Type = FieldType.Separator,
                Order = (lastField?.Order ?? 0) + 1
            });
        }

        private void RemoveField(FieldDefinition? field)
        {
            if (field != null)
            {
                var result = MessageBox.Show(
                    $"¿Realmente desea eliminar el campo [{field.Label}]?",
                    "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    Fields.Remove(field);
                }
            }
        }

        private void MoveUp(FieldDefinition? field)
        {
            if (field == null) return;
            int index = Fields.IndexOf(field);
            if (index > 0)
            {
                Fields.Move(index, index - 1);
            }
        }

        private void MoveDown(FieldDefinition? field)
        {
            if (field == null) return;
            int index = Fields.IndexOf(field);
            if (index < Fields.Count - 1)
            {
                Fields.Move(index, index + 1);
            }
        }

        private void ConfigureOptions(FieldDefinition? field)
        {
            if (field == null || field.Type != FieldType.Dropdown) return;

            var vm = new OptionsEditorViewModel(field.Options);
            var win = new OptionsWindow
            {
                DataContext = vm,
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            };

            if (win.ShowDialog() == true)
            {
                field.Options = vm.Options.ToList();
                // Notificar cambio en OptionsString para que se vea si fuera necesario (aunque ya no usaremos el campo de texto)
                OnPropertyChanged(nameof(Fields));
            }
        }

        private void SaveConfig()
        {
            IsSaveSuccessful = false;

            // 1. Validar etiquetas vacías
            var emptyLabels = Fields.Where(f => string.IsNullOrWhiteSpace(f.Label)).ToList();
            if (emptyLabels.Any())
            {
                MessageBox.Show("Todos los campos y cuadros deben tener un nombre. No pueden quedar vacíos.",
                    "Error de validación", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 2. Validar opciones de dropdowns
            var emptyDropdowns = Fields.Where(f => f.Type == FieldType.Dropdown && string.IsNullOrWhiteSpace(f.OptionsString)).ToList();
            if (emptyDropdowns.Any())
            {
                MessageBox.Show($"Los campos de tipo Dropdown deben tener opciones (ej: Cumple, No cumple). Revisar el campo: {emptyDropdowns.First().Label}",
                    "Error de validación", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3. Validar nombres duplicados
            var duplicates = Fields.GroupBy(f => f.Label.Trim().ToLower())
                                   .Where(g => g.Count() > 1)
                                   .Select(g => g.First().Label)
                                   .ToList();

            if (duplicates.Any())
            {
                MessageBox.Show(
                    $"No se pueden guardar los cambios porque hay nombres o cuadros duplicados:\n- {string.Join("\n- ", duplicates)}",
                    "Error de validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // El orden ahora es el de la colección visual
            var list = Fields.ToList();

            string currentBoxName = "General";

            for (int i = 0; i < list.Count; i++)
            {
                var field = list[i];
                field.Order = i;

                if (field.Type == FieldType.Separator)
                {
                    currentBoxName = field.Label;
                    field.Category = "--- SECCIÓN ---";
                }
                else
                {
                    field.Category = currentBoxName;
                    field.EnsureDefaultOptions();
                }
            }

            _storageService.SaveConfiguration(list);
            IsSaveSuccessful = true;
        }
    }
}
