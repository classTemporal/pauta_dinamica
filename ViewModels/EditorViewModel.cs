using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;
using PautaDinamicaApp.Views;
using Microsoft.Win32;
using System.Text.Json;
using System.IO;

namespace PautaDinamicaApp.ViewModels
{
    public class EditorViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;
        private ObservableCollection<FieldDefinition> _fields;

        private bool _hasExistingRecords;
        private bool _isMultiSelectMode;
        private string _initialFieldsJson = string.Empty;
        public bool ShouldClearRecords { get; private set; }

        public EditorViewModel(bool hasExistingRecords = false)
        {
            _hasExistingRecords = hasExistingRecords;
            _storageService = new StorageService();
            var config = _storageService.LoadConfiguration()
                            .OrderBy(f => f.Order);

            var list = config.ToList();
            foreach (var f in list) f.EnsureDefaultOptions();
            _fields = new ObservableCollection<FieldDefinition>(list);

            // Guardar estado inicial para detectar cambios estructurales
            _initialFieldsJson = JsonSerializer.Serialize(_fields);

            AddFieldCommand = new RelayCommand(_ => AddField());
            AddSectionCommand = new RelayCommand(_ => AddSection());
            RemoveFieldCommand = new RelayCommand(p => RemoveField(p as FieldDefinition));
            MoveUpCommand = new RelayCommand(p => MoveUp(p as FieldDefinition));
            MoveDownCommand = new RelayCommand(p => MoveDown(p as FieldDefinition));
            ConfigureOptionsCommand = new RelayCommand(p => ConfigureOptions(p as FieldDefinition));
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            ExportConfigCommand = new RelayCommand(_ => ExportConfig());
            ImportConfigCommand = new RelayCommand(_ => ImportConfig());
            ToggleMultiSelectCommand = new RelayCommand(_ => ToggleMultiSelect());
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected());

            // Tipos disponibles para el Combo
            AvailableTypes = Enum.GetValues(typeof(FieldType)).Cast<FieldType>().ToList();
        }

        public bool IsSaveSuccessful { get; private set; }

        public ObservableCollection<FieldDefinition> Fields
        {
            get => _fields;
            set => SetProperty(ref _fields, value);
        }

        public bool IsMultiSelectMode
        {
            get => _isMultiSelectMode;
            set => SetProperty(ref _isMultiSelectMode, value);
        }

        public System.Collections.Generic.List<FieldType> AvailableTypes { get; }

        public ICommand AddFieldCommand { get; }
        public ICommand AddSectionCommand { get; }
        public ICommand RemoveFieldCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand ConfigureOptionsCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand ExportConfigCommand { get; }
        public ICommand ImportConfigCommand { get; }
        public ICommand ToggleMultiSelectCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeleteSelectedCommand { get; }

        private string GetNextAvailableLabel(string baseName)
        {
            var existingLabels = Fields.Select(f => f.Label).ToList();

            // Check if base name exists
            if (!existingLabels.Contains(baseName)) return baseName;

            int i = 2;
            while (true)
            {
                string candidate = $"{baseName} {i}";
                if (!existingLabels.Contains(candidate))
                {
                    return candidate;
                }
                i++;
            }
        }

        private void AddField()
        {
            var lastField = Fields.OrderBy(f => f.Order).LastOrDefault();
            Fields.Add(new FieldDefinition
            {
                Id = "f_" + Guid.NewGuid().ToString().Substring(0, 4),
                Label = GetNextAvailableLabel("Nuevo Campo"),
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
                Label = GetNextAvailableLabel("Nuevo Cuadro"),
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
                field.Options = vm.ResultOptions;
                // Notificar cambio en OptionsString para que se vea si fuera necesario (aunque ya no usaremos el campo de texto)
                OnPropertyChanged(nameof(Fields));
            }
        }

        private void ExportConfig()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = $"PautaConfig_{DateTime.Now:yyyyMMdd}.json"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string json = JsonSerializer.Serialize(Fields, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(sfd.FileName, json);
                    MessageBox.Show("Configuración exportada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportConfig()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var imported = JsonSerializer.Deserialize<ObservableCollection<FieldDefinition>>(json);
                    if (imported != null)
                    {
                        var result = MessageBox.Show(
                            "¿Está seguro de que desea importar esta configuración? Esto reemplazará su diseño actual.",
                            "Confirmar Importación",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            Fields = imported;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al importar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ToggleMultiSelect()
        {
            IsMultiSelectMode = !IsMultiSelectMode;
            if (!IsMultiSelectMode)
            {
                foreach (var f in Fields) f.IsSelected = false;
            }
        }

        private void SelectAll()
        {
            bool allSelected = Fields.All(f => f.IsSelected);
            foreach (var f in Fields) f.IsSelected = !allSelected;
            // Refrescar la vista para mostrar los checks
            OnPropertyChanged(nameof(Fields));
        }

        private void DeleteSelected()
        {
            var selected = Fields.Where(f => f.IsSelected).ToList();
            if (!selected.Any()) return;

            var result = MessageBox.Show(
                $"¿Desea eliminar los {selected.Count} elementos seleccionados?",
                "Confirmar eliminación múltiple",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var item in selected)
                {
                    Fields.Remove(item);
                }
            }
        }

        private void SaveConfig()
        {
            // Reset state
            IsSaveSuccessful = false;
            ShouldClearRecords = false;

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

            // DETECCION DE CAMBIOS REALES
            string currentFieldsJson = JsonSerializer.Serialize(Fields);
            bool hasStructuralChanges = _initialFieldsJson != currentFieldsJson;

            if (_hasExistingRecords && hasStructuralChanges)
            {
                var result = MessageBox.Show(
                    "Se han detectado cambios en la estructura de la pauta y existen registros actuales.\r\n\r\n" +
                    "Para mantener la integridad de los datos, se reiniciará la base de datos.\r\n" +
                    "Se generarán respaldos automáticos tanto en Excel como en JSON de sus datos actuales.\r\n\r\n" +
                    "¿Desea aplicar los cambios?",
                    "Cambio de Estructura Detectado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                ShouldClearRecords = true;
            }

            // Guardar configuración solo si hubo cambios o si es necesario limpiar registros
            if (hasStructuralChanges)
            {
                _storageService.BackupConfiguration();
                _storageService.SaveConfiguration(list);
            }

            IsSaveSuccessful = true;
        }
    }
}
