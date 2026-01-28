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
            set => SetProperty(ref _currentFields, value);
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
            set { if (SetProperty(ref _selectedRecord, value)) OnPropertyChanged(nameof(IsEditMode)); }
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
        public bool IsMultiSelectMode { get => _isMultiSelectMode; set => SetProperty(ref _isMultiSelectMode, value); }

        private void LoadData()
        {
            RefreshFields();
            var savedRecords = _storageService.LoadRecords();
            Records = new ObservableCollection<AuditEntry>(savedRecords);
            FieldsRefreshed?.Invoke();
        }

        public void RefreshFields()
        {
            var config = _storageService.LoadConfiguration().OrderBy(f => f.Order).ToList();
            var fields = config.Where(c => c.Type != FieldType.Separator).Select(c => new DynamicFieldVM(c)).ToList();

            foreach (var f in fields)
            {
                f.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(DynamicFieldVM.Value) && !_isCalculating)
                    {
                        RefreshCalculations();
                    }
                };
            }

            CurrentFields = new ObservableCollection<DynamicFieldVM>(fields);
            _groupedFields = CollectionViewSource.GetDefaultView(CurrentFields);
            _groupedFields.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DynamicFieldVM.Category)));
            OnPropertyChanged(nameof(GroupedFields));

            RefreshCalculations();
            FieldsRefreshed?.Invoke();
        }

        private bool _isCalculating;
        private void RefreshCalculations()
        {
            if (_isCalculating) return;
            _isCalculating = true;
            try
            {
                // 1. CÁLCULO DE PORCENTAJES (Lógica de Unidades de Auditoría)
                foreach (var calcField in CurrentFields.Where(f => f.Type == FieldType.Calculation))
                {
                    double totalPossibleWeights = 0;
                    double totalEarnedWeights = 0;
                    var rules = calcField.Definition.ScoringRules;

                    // Si hay reglas configuradas, solo evaluamos esos campos. 
                    // Si no hay reglas, evaluamos todos los Dropdowns y Checkboxes (Modo Auto).
                    bool useManualRules = rules.Any();

                    var candidates = CurrentFields.Where(f => f.Id != calcField.Id && (f.Type == FieldType.Dropdown || f.Type == FieldType.Boolean));

                    foreach (var source in candidates)
                    {
                        var rule = rules.FirstOrDefault(r => r.FieldId == source.Id);
                        if (useManualRules && rule == null) continue;

                        string currentVal = source.Value?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(currentVal)) continue;

                        // Manejo de N/A: Se saca el campo COMPLETAMENTE de la pauta.
                        string naText = rule?.NaValue ?? "N/A";
                        if (currentVal.Equals(naText, StringComparison.OrdinalIgnoreCase)) continue;

                        double weight = calcField.Definition.UseCustomWeights ? (rule?.Weight ?? 1.0) : 1.0;
                        double earnedNormalized = 0; // Escala 0.0 a 1.0 (unidad)

                        if (rule != null && rule.Mappings.Any())
                        {
                            // Comparar el valor seleccionado con los mapeos.
                            // Nota: Para Booleans, currentVal será "True" o "False".
                            var mapping = rule.Mappings.FirstOrDefault(m =>
                                m.Value.Equals(currentVal, StringComparison.OrdinalIgnoreCase) ||
                                (source.Type == FieldType.Boolean && m.Value.Split(' ')[0].Equals(currentVal, StringComparison.OrdinalIgnoreCase))
                            );

                            if (mapping != null) earnedNormalized = mapping.Score;
                        }
                        else
                        {
                            // Modo automático: Solo "Cumple" o "True" suman el punto completo.
                            if (currentVal.Equals("Cumple", StringComparison.OrdinalIgnoreCase) ||
                               (source.Type == FieldType.Boolean && source.Value is bool b && b))
                            {
                                earnedNormalized = 1.0;
                            }
                        }

                        // CADA CAMPO ES UNA UNIDAD (1.0). El porcentaje se basa en qué fracción de esa unidad se obtuvo.
                        totalEarnedWeights += earnedNormalized * weight;
                        totalPossibleWeights += 1.0 * weight;
                    }

                    calcField.Value = totalPossibleWeights > 0
                        ? $"{(totalEarnedWeights / totalPossibleWeights * 100):F2}%"
                        : "0.00%";
                }

                // 2. CÁLCULO DE PROMEDIOS (AVERAGE)
                foreach (var avgField in CurrentFields.Where(f => f.Type == FieldType.Average))
                {
                    var targets = CurrentFields.Where(f => avgField.Definition.TargetIds.Contains(f.Id)).ToList();
                    double sum = 0;
                    int count = 0;
                    foreach (var t in targets)
                    {
                        string valText = t.Value?.ToString()?.Replace("%", "") ?? "";
                        if (double.TryParse(valText, out double d)) { sum += d; count++; }
                    }
                    avgField.Value = count > 0 ? $"{(sum / count):F2}%" : "0.00%";
                }
            }
            finally { _isCalculating = false; }
        }

        private void OpenConfiguration()
        {
            var hasRecords = Records.Any();
            var win = new ConfigWindow(hasRecords);
            win.Owner = Application.Current.MainWindow;
            if (win.ShowDialog() == true) RefreshFields();
        }

        private void CreateNewRecord()
        {
            SelectedRecord = null;
            foreach (var field in CurrentFields) field.Reset();
            OnPropertyChanged(nameof(IsEditMode));
        }

        private void LoadRecordToForm(AuditEntry? record)
        {
            if (record == null) return;
            foreach (var field in CurrentFields)
            {
                if (record.Values.TryGetValue(field.Id, out var value)) field.Value = value;
                else field.Value = null;
            }
        }

        private bool CanSaveRecord() => true;

        private void SaveCurrentRecord()
        {
            foreach (var field in CurrentFields) field.Validate();
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

            if (SelectedRecord == null) Records.Add(entry);
            entry.NotifyUpdate();
            _storageService.SaveRecords(Records.ToList());
            foreach (var field in CurrentFields) field.Reset();
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
            if (MessageBox.Show("¿Eliminar registro?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Records.Remove(entry);
                _storageService.SaveRecords(Records.ToList());
                if (SelectedRecord == entry) CreateNewRecord();
            }
        }

        private void DeleteAllRecords()
        {
            if (MessageBox.Show("¿Eliminar TODOS?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Records.Clear();
                _storageService.SaveRecords(Records.ToList());
                CreateNewRecord();
            }
        }

        public void ExportRecordsToExcel(IEnumerable<AuditEntry>? recordsToExport = null, string? customTitle = null)
        {
            var data = recordsToExport ?? Records;
            if (!data.Any()) return;
            var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = customTitle ?? $"Auditoria_{DateTime.Now:yyyyMMdd_HHmm}" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Auditoría");
                        worksheet.Cell(1, 1).Value = "Fecha";
                        var fields = CurrentFields.ToList();
                        for (int i = 0; i < fields.Count; i++) worksheet.Cell(1, i + 2).Value = fields[i].Label;
                        int row = 2;
                        foreach (var entry in data)
                        {
                            worksheet.Cell(row, 1).Value = entry.Timestamp.ToString("g");
                            for (int i = 0; i < fields.Count; i++)
                            {
                                if (entry.Values.TryGetValue(fields[i].Id, out var val)) worksheet.Cell(row, i + 2).Value = val?.ToString() ?? "";
                            }
                            row++;
                        }
                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(sfd.FileName);
                    }
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        public void ExportRecordsToJson(IEnumerable<AuditEntry>? recordsToExport = null, string? customTitle = null)
        {
            var data = recordsToExport ?? Records;
            if (!data.Any()) return;
            var sfd = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", FileName = customTitle ?? $"Auditoria_JSON_{DateTime.Now:yyyyMMdd_HHmm}" };
            if (sfd.ShowDialog() == true)
            {
                try { File.WriteAllText(sfd.FileName, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })); }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void ToggleMultiSelect()
        {
            IsMultiSelectMode = !IsMultiSelectMode;
            if (!IsMultiSelectMode) foreach (var rec in Records) rec.IsSelected = false;
        }

        private void ExecuteSelectAll()
        {
            bool all = Records.All(r => r.IsSelected);
            foreach (var rec in Records) rec.IsSelected = !all;
        }

        private void DeleteSelectedRecords()
        {
            var selected = Records.Where(r => r.IsSelected).ToList();
            if (!selected.Any()) return;
            if (MessageBox.Show($"¿Eliminar {selected.Count}?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var rec in selected) Records.Remove(rec);
                _storageService.SaveRecords(Records.ToList());
                CreateNewRecord();
            }
        }
    }
}
