using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using System.ComponentModel;
using System.Text.Json;
using System.IO;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;
using ClosedXML.Excel;
using Microsoft.Win32;
using System.Collections.Generic;

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
            ExportToExcelCommand = new RelayCommand(_ => ExportRecordsToExcel());
            ExportToJsonCommand = new RelayCommand(_ => ExportRecordsToJson());
            ToggleMultiSelectCommand = new RelayCommand(_ => ToggleMultiSelect());
            SelectAllCommand = new RelayCommand(_ => ExecuteSelectAll());
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelectedRecords());
            ExportSelectedCommand = new RelayCommand(_ => ExportRecordsToExcel(Records.Where(r => r.IsSelected).ToList(), "Export_Parcial_Auditoria"));
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
        public ICommand ExportToExcelCommand { get; }
        public ICommand ExportToJsonCommand { get; }
        public ICommand ToggleMultiSelectCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ExportSelectedCommand { get; }

        private bool _isMultiSelectMode;
        public bool IsMultiSelectMode
        {
            get => _isMultiSelectMode;
            set => SetProperty(ref _isMultiSelectMode, value);
        }

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
            var hasRecords = Records.Any();
            var win = new ConfigWindow(hasRecords);
            win.Owner = Application.Current.MainWindow;

            if (win.ShowDialog() == true)
            {
                if (win.DataContext is EditorViewModel editorVm && editorVm.ShouldClearRecords)
                {
                    MessageBox.Show("Se requiere realizar respaldos de seguridad antes de aplicar los cambios estructurales. Por favor, asigne una ubicación para el Excel y luego para el JSON de respaldo.",
                                    "Respaldo Obligatorio", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Respaldar antes de borrar (obligatorio - EXCEL)
                    ExportRecordsToExcel(Records, $"Respaldo_Excel_Pauta_Anterior_{DateTime.Now:yyyyMMdd_HHmm}");

                    // Respaldar antes de borrar (obligatorio - JSON)
                    ExportRecordsToJson(Records, $"Respaldo_JSON_Pauta_Anterior_{DateTime.Now:yyyyMMdd_HHmm}");

                    // Borrar registros
                    Records.Clear();
                    _storageService.SaveRecords(Records.ToList());
                    CreateNewRecord();
                }

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

        public void ExportRecordsToExcel(IEnumerable<AuditEntry>? recordsToExport = null, string? customTitle = null)
        {
            var data = recordsToExport ?? Records;
            if (!data.Any())
            {
                MessageBox.Show("No hay registros para exportar.", "Exportar a Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = customTitle ?? $"Auditoria_{DateTime.Now:yyyyMMdd_HHmm}"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Auditoría");

                        // Headers
                        worksheet.Cell(1, 1).Value = "Fecha";
                        int col = 2;

                        // Get all fields that have values in the records, or all current fields
                        var fields = CurrentFields.ToList();
                        foreach (var field in fields)
                        {
                            worksheet.Cell(1, col++).Value = field.Label;
                        }

                        // Styling headers
                        var headerRange = worksheet.Range(1, 1, 1, col - 1);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#007bff");
                        headerRange.Style.Font.FontColor = XLColor.White;

                        // Data
                        int row = 2;
                        foreach (var entry in data)
                        {
                            worksheet.Cell(row, 1).Value = entry.Timestamp.ToString("g");
                            int c = 2;
                            foreach (var field in fields)
                            {
                                if (entry.Values.TryGetValue(field.Id, out var val))
                                {
                                    worksheet.Cell(row, c).Value = val?.ToString() ?? "";
                                }
                                c++;
                            }
                            row++;
                        }

                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(sfd.FileName);
                    }

                    if (customTitle == null) // Only show success if it's a manual export, not a background backup
                    {
                        MessageBox.Show("Archivo Excel generado con éxito.", "Exportar a Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al generar el Excel: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void ExportRecordsToJson(IEnumerable<AuditEntry>? recordsToExport = null, string? customTitle = null)
        {
            // Si no nos pasan datos y hay selección múltiple activa, priorizamos los seleccionados
            var data = recordsToExport?.ToList() ??
                       (IsMultiSelectMode ? Records.Where(r => r.IsSelected).ToList() : Records.ToList());

            if (!data.Any())
            {
                MessageBox.Show("No hay registros para exportar. Asegúrese de seleccionar elementos si está en modo selección.", "Exportar a JSON", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = customTitle ?? $"Auditoria_Datos_{DateTime.Now:yyyyMMdd_HHmm}"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(sfd.FileName, json);

                    if (customTitle == null)
                    {
                        MessageBox.Show("Datos exportados a JSON correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al exportar a JSON: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ToggleMultiSelect()
        {
            IsMultiSelectMode = !IsMultiSelectMode;
            // Clear selection when exiting mode
            if (!IsMultiSelectMode)
            {
                foreach (var rec in Records) rec.IsSelected = false;
            }
        }

        private void ExecuteSelectAll()
        {
            // Toggle all based on whether all are currently selected
            bool allSelected = Records.All(r => r.IsSelected);
            foreach (var rec in Records)
            {
                rec.IsSelected = !allSelected;
            }
        }

        private void DeleteSelectedRecords()
        {
            var selected = Records.Where(r => r.IsSelected).ToList();
            if (!selected.Any()) return;

            var result = MessageBox.Show($"¿Eliminar {selected.Count} registros seleccionados?",
                                       "Confirmar Eliminación Múltiple",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var rec in selected)
                {
                    Records.Remove(rec);
                }
                _storageService.SaveRecords(Records.ToList());
                CreateNewRecord(); // Reset form just in case
            }
        }
    }
}
