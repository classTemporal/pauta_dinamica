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
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;

namespace PautaDinamicaApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public event Action? FieldsRefreshed;
        private readonly StorageService _storageService;
        private readonly PdfService _pdfService; // Nuevo servicio
        private ObservableCollection<DynamicFieldVM> _currentFields = new();
        private ICollectionView? _groupedFields;
        private ObservableCollection<AuditEntry> _records = new();
        private AuditEntry? _selectedRecord;
        private ObservableCollection<PautaSchema> _pautas = new();
        private PautaSchema? _currentPauta;

        public ICommand OpenSettingsCommand { get; }


        public MainViewModel()
        {
            _storageService = new StorageService();
            _pdfService = new PdfService();
            LoadPautas();
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
            ImportFromExcelCommand = new RelayCommand(_ => ImportRecordsFromExcel());
            SendEmailsCommand = new RelayCommand(_ => SendEmails());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());

            // Comandos PDF
            GeneratePdfCommand = new RelayCommand(p => GeneratePdfForRecord(p as AuditEntry));
            GenerateSelectedPdfCommand = new RelayCommand(_ => GeneratePdfForSelected());
        }

        private void OpenSettings()
        {
            var vm = new SettingsViewModel();
            var win = new Views.SettingsWindow { DataContext = vm, Owner = System.Windows.Application.Current.MainWindow };
            vm.RequestClose += () => win.Close();
            win.ShowDialog();
        }

        public bool ExportRecordsToExcel(IEnumerable<AuditEntry>? recordsToExport = null, string? customTitle = null, bool silent = false)
        {
            var data = (recordsToExport ?? Records).ToList();
            if (!data.Any()) return false;

            string filePath;
            if (silent)
            {
                string backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                filePath = Path.Combine(backupDir, $"{customTitle ?? "Backup"}.xlsx");
            }
            else
            {
                var settings = _storageService.LoadSettings();
                string exportDir = settings.ExcelExportPath;
                if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

                string fileName = (customTitle ?? $"Auditoria_{DateTime.Now:yyyyMMdd_HHmm}") + ".xlsx";
                filePath = Path.Combine(exportDir, fileName);
            }

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
                        int col = 2;
                        foreach (var f in fields)
                        {
                            if (entry.Values.TryGetValue(f.Id, out var val))
                            {
                                string strVal = val?.ToString() ?? "";
                                var cell = worksheet.Cell(row, col);
                                cell.Value = strVal;

                                if (f.Type == FieldType.TextArea || strVal.Contains("\n"))
                                {
                                    cell.Style.Alignment.SetWrapText(true);
                                }
                            }
                            col++;
                        }
                        row++;
                    }
                    worksheet.Columns().AdjustToContents();
                    foreach (var col in worksheet.Columns())
                    {
                        if (col.Width > 50) col.Width = 50;
                    }
                    workbook.SaveAs(filePath);
                }
                if (!silent) MessageBox.Show("Exportación a Excel exitosa.");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar Excel: {ex.Message}");
                return false;
            }
        }

        public bool ExportRecordsToJson(IEnumerable<AuditEntry>? recordsToExport = null, string? customTitle = null)
        {
            var data = (recordsToExport ?? Records).ToList();
            if (!data.Any()) return false;

            var settings = _storageService.LoadSettings();
            string exportDir = settings.JsonBackupPath;
            if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

            string fileName = (customTitle ?? $"Respaldo_{DateTime.Now:yyyyMMdd_HHmm}") + ".json";
            string filePath = Path.Combine(exportDir, fileName);

            try
            {
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                MessageBox.Show($"Exportación a JSON exitosa.\n\nArchivo guardado en:\n{filePath}");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar JSON: {ex.Message}");
                return false;
            }
        }

        private void ImportRecordsFromExcel()
        {
            var settings = _storageService.LoadSettings();
            var ofd = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                InitialDirectory = settings.ExcelExportPath
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new XLWorkbook(ofd.FileName))
                    {
                        var worksheet = workbook.Worksheets.FirstOrDefault();
                        if (worksheet == null) return;

                        var rows = worksheet.RowsUsed().Skip(1);
                        var headers = worksheet.Row(1).CellsUsed().ToDictionary(c => c.Address.ColumnNumber, c => c.Value.ToString().Trim());
                        var fields = CurrentFields.ToList();

                        var excelHeaderNames = headers.Values.ToList();
                        var appFieldNames = fields.Select(f => f.Label).ToList();
                        var commonFields = appFieldNames.Intersect(excelHeaderNames, StringComparer.OrdinalIgnoreCase).ToList();

                        if (!commonFields.Any())
                        {
                            MessageBox.Show("El archivo Excel no es compatible con la pauta actual.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        foreach (var row in rows)
                        {
                            var entry = new AuditEntry();
                            bool rowHasData = false;
                            if (headers.TryGetValue(1, out var firstHeader) && firstHeader.Equals("Fecha", StringComparison.OrdinalIgnoreCase))
                            {
                                if (DateTime.TryParse(row.Cell(1).Value.ToString(), out var dt)) entry.Timestamp = dt;
                            }
                            foreach (var header in headers)
                            {
                                var field = fields.FirstOrDefault(f => f.Label.Equals(header.Value, StringComparison.OrdinalIgnoreCase));
                                if (field != null)
                                {
                                    entry.Values[field.Id] = row.Cell(header.Key).Value.ToString();
                                    rowHasData = true;
                                }
                            }
                            if (rowHasData) Records.Add(entry);
                        }
                        if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
                        MessageBox.Show("Importación exitosa.");
                        RefreshCalculations();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al importar: {ex.Message}");
                }
            }
        }

        private void GenerateBatchPdfs(List<AuditEntry> records)
        {
            try
            {
                var settings = _storageService.LoadSettings();
                string folderPath = settings.PdfReportPath;
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                string pautaName = CurrentPauta?.Name ?? "Auditoria";
                string safePautaName = string.Join("_", pautaName.Split(Path.GetInvalidFileNameChars()));
                var definitions = CurrentFields.Select(f => f.Definition).ToList();
                int count = 0;

                foreach (var record in records)
                {
                    string timestamp = record.Timestamp.ToString("yyyyMMdd_HHmmss");
                    string filename = $"Reporte_{safePautaName}_{timestamp}_{count + 1}.pdf";
                    string fullPath = Path.Combine(folderPath, filename);

                    _pdfService.GenerateAuditPdf(new List<AuditEntry> { record }, definitions, pautaName, fullPath);
                    count++;
                }

                if (MessageBox.Show($"Se generaron {count} PDFs en:\n{folderPath}\n\n¿Abrir carpeta?", "Éxito", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar PDFs: {ex.Message}");
            }
        }

        private void GeneratePdfCommon(List<AuditEntry> records)
        {
            try
            {
                string pautaName = CurrentPauta?.Name ?? "Auditoria";
                var definitions = CurrentFields.Select(f => f.Definition).ToList();
                var settings = _storageService.LoadSettings();
                string exportDir = settings.PdfReportPath;
                if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

                string fileName = $"Reporte_{pautaName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string filePath = Path.Combine(exportDir, fileName);

                _pdfService.GenerateAuditPdf(records, definitions, pautaName, filePath);

                if (MessageBox.Show($"PDF Generado con éxito en:\n{filePath}\n\n¿Abrir ahora?", "Éxito", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true } }.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
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

        public ObservableCollection<PautaSchema> Pautas
        {
            get => _pautas;
            set => SetProperty(ref _pautas, value);
        }

        public PautaSchema? CurrentPauta
        {
            get => _currentPauta;
            set
            {
                if (SetProperty(ref _currentPauta, value) && value != null)
                {
                    _storageService.SetLastPautaId(value.Id);
                    LoadData();
                }
            }
        }

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
        public ICommand ImportFromExcelCommand { get; }
        public ICommand SendEmailsCommand { get; }
        public ICommand GeneratePdfCommand { get; }
        public ICommand GenerateSelectedPdfCommand { get; }

        private bool _isMultiSelectMode;
        public bool IsMultiSelectMode { get => _isMultiSelectMode; set => SetProperty(ref _isMultiSelectMode, value); }

        private void LoadPautas()
        {
            var pautas = _storageService.LoadPautas();
            string lastId = _storageService.GetLastPautaId();

            Pautas = new ObservableCollection<PautaSchema>(pautas);
            CurrentPauta = Pautas.FirstOrDefault(p => p.Id == lastId) ?? Pautas.FirstOrDefault();
        }

        private void LoadData()
        {
            if (CurrentPauta == null) return;
            RefreshFields();
            var savedRecords = _storageService.LoadRecords(CurrentPauta.Id);
            Records = new ObservableCollection<AuditEntry>(savedRecords);
            FieldsRefreshed?.Invoke();
            CreateNewRecord();
        }

        public void RefreshFields()
        {
            if (CurrentPauta == null) return;
            var config = _storageService.LoadConfiguration(CurrentPauta.Id).OrderBy(f => f.Order).ToList();
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
                        ? $"{(totalEarnedWeights / totalPossibleWeights * 100):F1}%"
                        : "0.0%";
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
                    avgField.Value = count > 0 ? $"{(sum / count):F1}%" : "0.0%";
                }
            }
            finally { _isCalculating = false; }
        }

        private void OpenConfiguration()
        {
            if (CurrentPauta == null) return;
            var hasRecords = Records.Any();
            var win = new ConfigWindow(CurrentPauta.Id);
            win.Owner = System.Windows.Application.Current.MainWindow;

            if (win.ShowDialog() == true)
            {
                LoadPautas(); // Recargar lista por si se agregaron/eliminaron pautas
                if (win.DataContext is EditorViewModel editorVm && editorVm.ShouldClearRecords)
                {
                    // Los respaldos ya se hicieron dentro del ConfigWindow.
                    // Aquí solo limpiamos y refrescamos la vista actual del MainViewModel.
                    Records.Clear();
                    RefreshFields();
                    MessageBox.Show("La vista se ha refrescado debido a cambios estructurales.");
                }
                else
                {
                    RefreshFields();
                }
            }


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
                string errors = string.Join("\n", CurrentFields.Where(f => !f.IsValid).Select(f => $"- {f.Label}: {f.ValidationError}"));
                MessageBox.Show($"Por favor, corrija los siguientes errores:\n\n{errors}", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- VALIDACIÓN DE DUPLICADOS ---
            foreach (var field in CurrentFields)
            {
                // Solo validamos si está activada la opción y hay un valor ingresado
                // Y si el tipo es Texto, Numérico o Área de Texto (evitar validar Dropdowns por defecto)
                var def = field.Definition;
                bool checkType = def.Type == FieldType.Text || def.Type == FieldType.Numeric || def.Type == FieldType.TextArea;

                if (checkType && def.WarnOnDuplicate && field.Value != null && !string.IsNullOrWhiteSpace(field.Value.ToString()))
                {
                    string currentValue = field.Value.ToString()!.Trim();

                    // Buscamos si existe algun otro registro con este valor en este campo
                    // Excluimos el registro actual si estamos en modo edición
                    bool isDuplicate = Records.Any(r =>
                        r != SelectedRecord && // No compararse consigo mismo
                        r.Values.TryGetValue(field.Definition.Id, out var val) && // Obtener valor del campo
                        val != null &&
                        string.Equals(val.ToString()!.Trim(), currentValue, StringComparison.OrdinalIgnoreCase)); // Comparar

                    if (isDuplicate)
                    {
                        var result = MessageBox.Show(
                            $"El valor '{currentValue}' en el campo '{field.Label}' ya existe en otro registro.\n\n¿Desea agregarlo de todas formas?",
                            "Valor Duplicado Detectado",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.No) return;
                    }
                }
            }

            var entry = SelectedRecord ?? new AuditEntry();
            foreach (var field in CurrentFields.Where(f => f.Type != FieldType.Separator))
            {
                entry.Values[field.Id] = field.Value ?? "";
            }

            if (SelectedRecord == null) Records.Add(entry);
            entry.NotifyUpdate();
            if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
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
                if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
                if (SelectedRecord == entry) CreateNewRecord();
            }
        }

        private void DeleteAllRecords()
        {
            if (MessageBox.Show("¿Eliminar TODOS los registros de esta pauta?", "Confirmar Eliminación Total", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Records.Clear();
                if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
                CreateNewRecord();
            }
        }

        private void GeneratePdfForRecord(AuditEntry? entry)
        {
            if (entry == null) return;
            GeneratePdfCommon(new List<AuditEntry> { entry });
        }

        private void GeneratePdfForSelected()
        {
            var selected = Records.Where(r => r.IsSelected).ToList();
            if (!selected.Any())
            {
                MessageBox.Show("No hay registros seleccionados.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selected.Count == 1) GeneratePdfCommon(selected);
            else GenerateBatchPdfs(selected);
        }

        private void SendEmails()
        {
            MessageBox.Show("Funcionalidad de correo electrónico próximamente.", "En Desarrollo");
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
                if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
                CreateNewRecord();
            }
        }

        // End of MainViewModel
    }
}
