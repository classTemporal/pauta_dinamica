using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using System.ComponentModel;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;

namespace PautaDinamicaApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public event Action? FieldsRefreshed;
        private readonly StorageService _storageService;
        private ObservableCollection<DynamicFieldVM> _currentFields = new();
        private ICollectionView? _groupedFields;
        private ObservableCollection<AuditEntry> _records = new();
        private AuditEntry? _selectedRecord;

        public MainViewModel()
        {
            _storageService = new StorageService();
            LoadData();

            SaveRecordCommand = new RelayCommand(_ => SaveCurrentRecord(), _ => CanSaveRecord());
            ClearFormCommand = new RelayCommand(_ => CreateNewRecord());
            SelectRecordCommand = new RelayCommand(p => EditRecord(p as AuditEntry));
            OpenConfigCommand = new RelayCommand(_ => OpenConfiguration());
            DeleteRecordCommand = new RelayCommand(p => DeleteRecord(p as AuditEntry));
            DeleteAllRecordsCommand = new RelayCommand(_ => DeleteAllRecords());
        }

        public ObservableCollection<DynamicFieldVM> CurrentFields
        {
            get => _currentFields;
            set
            {
                if (SetProperty(ref _currentFields, value))
                {
                    OnPropertyChanged(nameof(GroupedFields));
                }
            }
        }

        public ICollectionView GroupedFields => _groupedFields ?? CollectionViewSource.GetDefaultView(CurrentFields);

        public ObservableCollection<AuditEntry> Records
        {
            get => _records;
            set => SetProperty(ref _records, value);
        }

        public AuditEntry? SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                if (SetProperty(ref _selectedRecord, value))
                {
                    OnPropertyChanged(nameof(IsEditMode));
                }
            }
        }

        public bool IsEditMode => SelectedRecord != null;

        public ICommand SaveRecordCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand SelectRecordCommand { get; }
        public ICommand OpenConfigCommand { get; }
        public ICommand DeleteRecordCommand { get; }
        public ICommand DeleteAllRecordsCommand { get; }

        private void LoadData()
        {
            RefreshFields();

            var savedRecords = _storageService.LoadRecords();
            Records = new ObservableCollection<AuditEntry>(savedRecords);

            FieldsRefreshed?.Invoke();
        }

        public void RefreshFields()
        {
            var config = _storageService.LoadConfiguration()
                            .OrderBy(f => f.Order).ToList();

            var fields = config
                            .Where(c => c.Type != FieldType.Separator)
                            .Select(c => new DynamicFieldVM(c)).ToList();

            CurrentFields = new ObservableCollection<DynamicFieldVM>(fields);

            _groupedFields = CollectionViewSource.GetDefaultView(CurrentFields);
            _groupedFields.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DynamicFieldVM.Category)));
            OnPropertyChanged(nameof(GroupedFields));

            FieldsRefreshed?.Invoke();
        }

        private void OpenConfiguration()
        {
            var win = new ConfigWindow();
            win.Owner = Application.Current.MainWindow;
            if (win.ShowDialog() == true)
            {
                RefreshFields();
            }
        }

        private void CreateNewRecord()
        {
            SelectedRecord = null;
            foreach (var field in CurrentFields)
            {
                field.Reset();
            }
            OnPropertyChanged(nameof(IsEditMode));
        }

        private void LoadRecordToForm(AuditEntry? record)
        {
            if (record == null) return;

            foreach (var field in CurrentFields)
            {
                if (record.Values.TryGetValue(field.Id, out var value))
                {
                    field.Value = value;
                }
                else
                {
                    field.Value = null;
                }
            }
        }

        private bool CanSaveRecord()
        {
            // We always return true to allow the user to click "Save" 
            // and see the validation errors if they haven't filled everything.
            return true;
        }

        private void SaveCurrentRecord()
        {
            // Trigger validation for all fields
            foreach (var field in CurrentFields)
            {
                field.Validate();
            }

            if (CurrentFields.Any(f => !f.IsValid))
            {
                MessageBox.Show("Por favor, completa todos los campos obligatorios.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var entry = SelectedRecord ?? new AuditEntry();

            foreach (var field in CurrentFields.Where(f => f.Type != FieldType.Separator))
            {
                entry.Values[field.Id] = field.Value ?? "";
            }

            if (SelectedRecord == null)
            {
                Records.Add(entry);
                SelectedRecord = entry;
            }

            // Notificar que los datos han cambiado para que la tabla se actualice
            entry.NotifyUpdate();

            _storageService.SaveRecords(Records.ToList());

            // Success! Reset the fields and clear styles
            foreach (var field in CurrentFields)
            {
                field.Reset();
            }
            SelectedRecord = null;

            MessageBox.Show("Registro guardado correctamente.");
        }

        private void EditRecord(AuditEntry? entry)
        {
            if (entry == null) return;
            SelectedRecord = entry;
            LoadRecordToForm(entry);
        }

        private void DeleteRecord(AuditEntry? entry)
        {
            if (entry == null) return;

            var result = MessageBox.Show("¿Realmente desea eliminar este registro?", "Confirmar eliminación",
                                       MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Records.Remove(entry);
                _storageService.SaveRecords(Records.ToList());

                if (SelectedRecord == entry)
                {
                    CreateNewRecord();
                }
            }
        }

        private void DeleteAllRecords()
        {
            if (!Records.Any()) return;

            var result = MessageBox.Show("¿Realmente desea eliminar TODOS los registros? Esta acción no se puede deshacer.",
                                       "Confirmar eliminación MASIVA",
                                       MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Records.Clear();
                _storageService.SaveRecords(Records.ToList());
                CreateNewRecord();
            }
        }
    }
}
