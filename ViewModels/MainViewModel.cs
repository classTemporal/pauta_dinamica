using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using System.ComponentModel;
using System.Text;
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
        private readonly PdfService _pdfService;
        private readonly EmailService _emailService;
        private ObservableCollection<DynamicFieldVM> _currentFields = new();
        private ICollectionView? _groupedFields;
        private ObservableCollection<AuditEntry> _records = new();
        private AuditEntry? _selectedRecord;
        private ObservableCollection<PautaSchema> _pautas = new();
        private PautaSchema? _currentPauta;

        public ICommand OpenSettingsCommand { get; }

        // Comandos existentes
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
        public ICommand GeneratePdfCommand { get; }
        public ICommand GenerateSelectedPdfCommand { get; }
        public ICommand SendEmailsCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand ShowGeneralHelpCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public UserModel? CurrentUser => SessionService.CurrentUser;

        private DateTime _currentAuditStartTime = DateTime.Now;

        public MainViewModel()
        {
            _storageService = new StorageService();
            _pdfService = new PdfService();
            _emailService = new EmailService();

            // Aplicar tema guardado del usuario al iniciar
            var savedSettings = _storageService.LoadSettings();
            new ThemeService().SetTheme(savedSettings.Theme);

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
            GeneratePdfCommand = new RelayCommand(p => GeneratePdfForRecord(p as AuditEntry));
            GenerateSelectedPdfCommand = new RelayCommand(_ => GeneratePdfForSelected());
            SendEmailsCommand = new RelayCommand(p => SendEmails(p as AuditEntry));

            // Nuevo comando para configuración general
            OpenSettingsCommand = new RelayCommand(_ => StartSettingsFlow());
            ShowHelpCommand = new RelayCommand(_ => ShowHelp());
            ShowGeneralHelpCommand = new RelayCommand(_ => ShowGeneralHelp());
            LogoutCommand = new RelayCommand(_ => Logout());
            CancelEditCommand = new RelayCommand(_ => CreateNewRecord());
            ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        }

        private void ToggleTheme()
        {
            var settings = _storageService.LoadSettings();
            var newTheme = settings.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;

            // Guardar en perfil de usuario actual
            settings.Theme = newTheme;
            _storageService.SaveSettings(settings);

            // Guardar también en perfil default para que persista en la pantalla de login
            try
            {
                var defaultStorage = new StorageService("default");
                var defaultSettings = defaultStorage.LoadSettings();
                defaultSettings.Theme = newTheme;
                defaultStorage.SaveSettings(defaultSettings);
            }
            catch { /* Ignorar error al guardar default */ }

            new ThemeService().SetTheme(newTheme);
        }


        private void Logout()
        {
            new SessionService().Logout();

            // Re-open login window
            var loginWin = new Views.LoginWindow();
            loginWin.Show();

            // Close current window
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is MainWindow)
                {
                    window.Close();
                    break;
                }
            }
        }

        private void ShowGeneralHelp()
        {
            var content = new StringBuilder();
            content.AppendLine("# 📘 Documentación del Sistema");
            content.AppendLine("");
            content.AppendLine("**Versión:** 1.1.0");
            content.AppendLine("**Creador:** Angel Gustavo Pacheco Manzanero");
            content.AppendLine("");
            content.AppendLine("### 🚀 Resumen del Sistema");
            content.AppendLine("Pauta Dinámica es una herramienta avanzada diseñada para la **Auditoría de Calidad** y el **Control de Procesos**. Su objetivo principal es permitir la creación de formularios 100% dinámicos, eliminando la dependencia de hojas de cálculo estáticas y automatizando la generación de reportes y envío de métricas.");
            content.AppendLine("");
            content.AppendLine("---");
            content.AppendLine("");
            content.AppendLine("## 💡 Guía de Uso");
            content.AppendLine("");
            content.AppendLine("### 1. Gestión de Pautas (Diseño)");
            content.AppendLine("En el botón **CONFIG. PAUTA** puedes crear la estructura de tus formularios:");
            content.AppendLine("- **Campos Dinámicos:** Agrega textos, números, fechas, menús desplegables y campos de cálculo.");
            content.AppendLine("- **Agrupación:** Usa el botón **BOX** para crear secciones visuales que organizan los campos.");
            content.AppendLine("- **Personalización:** Marca campos como obligatorios o haz que conserven su valor al limpiar el formulario.");
            content.AppendLine("- **Instrucciones:** En la pestaña 'Instrucciones de Apoyo' puedes dejar guías específicas para cada pauta.");
            content.AppendLine("");
            content.AppendLine("### 2. Registro de Datos");
            content.AppendLine("- Selecciona una pauta en el menú superior izquierdo.");
            content.AppendLine("- Completa los campos en el panel izquierdo y presiona **Guardar Registro**.");
            content.AppendLine("- Los registros aparecerán en la tabla central de la derecha.");
            content.AppendLine("");
            content.AppendLine("### 3. Exportación y Reportes");
            content.AppendLine("- **Excel/JSON:** Exporta toda la base de datos o registros seleccionados a formatos editables.");
            content.AppendLine("- **PDF:** Genera reportes visuales con un solo clic. Puedes configurar la carpeta de salida en **CONFIG. GENERAL**.");
            content.AppendLine("");
            content.AppendLine("### 4. Sistema de Correos y Directorio");
            content.AppendLine("- **Envío Individual/Masivo:** Selecciona registros y presiona el icono de sobre para enviar correos pre-formateados.");
            content.AppendLine("- **Directorio de Agentes:** En la configuración general, puedes asociar nombres de agentes con sus correos para que el sistema los detecte automáticamente.");
            content.AppendLine("- **Plantillas:** Personaliza el asunto y cuerpo del mensaje usando `[Nombre del Campo]` como comodín.");
            content.AppendLine("");
            content.AppendLine("### 5. Resaltado Visual");
            content.AppendLine("- Puedes hacer que las filas de la tabla cambien de color automáticamente si un campo (ej: 'Calificación') alcanza un valor específico (ej: '100%'). Esto se configura en **CONFIG. GENERAL > Rutas**.");
            content.AppendLine("");
            content.AppendLine("---");
            content.AppendLine("");
            content.AppendLine("## 🔗 Enlaces del Desarrollador");
            content.AppendLine("");
            content.AppendLine("- **LinkedIn:** [Angel Temporal Pacheco](https://www.linkedin.com/in/angel-temporal-pacheco/)");
            content.AppendLine("- **GitHub:** [classTemporal](https://github.com/classTemporal)");
            content.AppendLine("");
            content.AppendLine("---");
            content.AppendLine("*Tip: Si tienes dudas sobre los criterios de una pauta específica, presiona el botón '?' circular junto al selector de pautas.*");

            var vm = new HelpViewModel("Documentación General", content.ToString());
            var win = new Views.HelpWindow { DataContext = vm };
            var owner = GetBestOwner();
            if (owner != null && owner != win) win.Owner = owner;
            win.ShowDialog();
        }

        private Window? GetBestOwner()
        {
            return System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()
                ?? System.Windows.Application.Current.MainWindow;
        }

        private void ShowHelp()
        {
            if (CurrentPauta == null) return;
            var vm = new HelpViewModel(CurrentPauta.Name, CurrentPauta.HelpContent);
            var win = new Views.HelpWindow { DataContext = vm };
            var owner = GetBestOwner();
            if (owner != null && owner != win) win.Owner = owner;
            win.ShowDialog();
        }

        private void StartSettingsFlow()
        {
            var vm = new SettingsViewModel();
            var settingsWin = new Views.SettingsWindow { DataContext = vm };
            var owner = GetBestOwner();
            if (owner != null && owner != settingsWin) settingsWin.Owner = owner;

            vm.RequestClose += () => settingsWin.Close();
            settingsWin.ShowDialog();

            if (vm.IsSaved)
            {
                LoadPautas();
                ApplyRowColoring();
                UpdateAuditStats();
            }
        }

        private void ApplyRowColoring()
        {
            var settings = _storageService.LoadSettings();
            string targetFieldLabel = settings.ColoringField;
            string targetValue = settings.ColoringValue;
            string targetColor = settings.ColoringColor;

            if (string.IsNullOrWhiteSpace(targetFieldLabel) || string.IsNullOrWhiteSpace(targetValue))
            {
                // Limpiar colores si no hay regla
                foreach (var r in Records) r.RowColor = null;
                return;
            }

            // Buscar ID del campo basado en el Label (Nombre)
            // Nota: Buscamos en CurrentFields, pero CurrentFields depende del registro seleccionado/nuevo.
            // Mejor usar la definición de la pauta cargada.
            if (CurrentPauta == null) return;
            var fields = _storageService.LoadConfiguration(CurrentPauta.Id);
            var targetField = fields.FirstOrDefault(f => f.Label.Equals(targetFieldLabel, StringComparison.OrdinalIgnoreCase));

            if (targetField == null) return; // Campo no encontrado

            foreach (var record in Records)
            {
                if (record.Values.TryGetValue(targetField.Id, out var val) && val != null)
                {
                    // Comparar valor (como string)
                    string strVal = val.ToString() ?? "";

                    // Manejo especial para JsonElement si es necesario (ya lo hace LoadData al desempaquetar, 
                    // pero el record en memoria puede tener JsonElement si no se ha editado)
                    if (val is System.Text.Json.JsonElement elem) strVal = elem.ToString();

                    // Comparación laxa
                    if (string.Equals(strVal.Trim(), targetValue.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        record.RowColor = targetColor;
                    }
                    else
                    {
                        record.RowColor = null;
                    }
                }
                else
                {
                    record.RowColor = null;
                }
            }
        }


        // Duplicate constructor removed
        // Orphaned code block removed.

        private string _auditStatsText = "";
        public string AuditStatsText
        {
            get => _auditStatsText;
            set => SetProperty(ref _auditStatsText, value);
        }

        private void UpdateAuditStats()
        {
            if (CurrentPauta == null || Records == null) return;

            var sb = new StringBuilder();
            sb.Append($" Total: {Records.Count} ");

            var counters = new[] {
                (CurrentPauta.CounterField1, CurrentPauta.CounterValue1),
                (CurrentPauta.CounterField2, CurrentPauta.CounterValue2),
                (CurrentPauta.CounterField3, CurrentPauta.CounterValue3)
            };

            foreach (var (fieldLabel, value) in counters)
            {
                if (string.IsNullOrWhiteSpace(fieldLabel) || string.IsNullOrWhiteSpace(value)) continue;

                if (CurrentPauta == null) break;

                var fields = _storageService.LoadConfiguration(CurrentPauta.Id);
                var targetField = fields.FirstOrDefault(f => f.Label.Equals(fieldLabel, StringComparison.OrdinalIgnoreCase));

                if (targetField != null)
                {
                    int count = Records.Count(r =>
                    {
                        if (r.Values.TryGetValue(targetField.Id, out var val) && val != null)
                        {
                            string strVal = val.ToString() ?? "";
                            if (val is System.Text.Json.JsonElement elem) strVal = elem.ToString();
                            return string.Equals(strVal.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);
                        }
                        return false;
                    });
                    sb.Append($" | {fieldLabel} ({value}): {count} ");
                }
            }
            AuditStatsText = sb.ToString();
        }

        private void OpenSettings()
        {
            var vm = new SettingsViewModel();
            var win = new Views.SettingsWindow { DataContext = vm, Owner = System.Windows.Application.Current.MainWindow };
            vm.RequestClose += () => win.Close();
            win.ShowDialog();

            if (vm.IsSaved)
            {
                LoadPautas(); // Refrescar para tener los nuevos métodos de envío, etc.
            }
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

                string pautaName = CurrentPauta?.Name ?? "Auditoria";
                string safePautaName = string.Join("_", pautaName.Split(Path.GetInvalidFileNameChars()));
                string fileName = (customTitle ?? $"{safePautaName}_{DateTime.Now:yyyyMMdd_HHmm}") + ".xlsx";
                filePath = Path.Combine(exportDir, fileName);
            }

            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Auditoría");

                    // --- DEFINIR COLUMNAS A EXPORTAR ---
                    // Usar configuración de exportación si existe, de lo contrario usar campos actuales en orden
                    var columnsToExport = new List<(string FieldId, string Header, FieldType Type, FieldDefinition? Def)>();

                    var settings = _storageService.LoadSettings();
                    var allConfig = CurrentPauta?.ExportConfig ?? new System.Collections.Generic.List<ExportColumnConfig>();
                    var exportConfig = allConfig.Where(c => c.IsExportEnabled).OrderBy(c => c.Order).ToList();

                    if (exportConfig != null && exportConfig.Any())
                    {
                        // 1. Duración: Solo si está activada GLOBALMENTE. 
                        // Se agrega SIEMPRE al principio si está habilitada, ignorando config de pauta.
                        if (settings.EnableInternalTimer)
                        {
                            columnsToExport.Add(("System_Duration", "Duración (min)", FieldType.Numeric, null));
                        }

                        foreach (var config in exportConfig)
                        {
                            // 2. Eliminar Fecha de evaluación (System_Timestamp)
                            if (config.FieldId == "System_Timestamp") continue;

                            // 3. Omitir System_Duration si ya está en la config (ya la agregamos arriba fija)
                            if (config.FieldId == "System_Duration") continue;

                            // 4. Campos dinámicos
                            var field = CurrentFields.FirstOrDefault(f => f.Id == config.FieldId);
                            if (field != null)
                            {
                                string header = !string.IsNullOrWhiteSpace(config.CustomHeader) ? config.CustomHeader : field.Label;
                                columnsToExport.Add((field.Id, header, field.Type, field.Definition));
                            }
                        }
                    }
                    else
                    {
                        // Fallback: Si no hay configuración manual, exportamos Duración (si aplica) y Campos Dinámicos
                        if (settings.EnableInternalTimer)
                        {
                            columnsToExport.Add(("System_Duration", "Duración (min)", FieldType.Numeric, null));
                        }

                        foreach (var field in CurrentFields)
                        {
                            columnsToExport.Add((field.Id, field.Label, field.Type, field.Definition));
                        }
                    }

                    // --- CABECERAS ---
                    for (int i = 0; i < columnsToExport.Count; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = columnsToExport[i].Header;
                    }

                    // --- DATOS ---
                    int row = 2;
                    foreach (var entry in data)
                    {
                        for (int i = 0; i < columnsToExport.Count; i++)
                        {
                            var (fieldId, header, type, def) = columnsToExport[i];
                            var cell = worksheet.Cell(row, i + 1);

                            // Manejo especial campos sistema
                            if (fieldId == "System_Timestamp")
                            {
                                cell.Value = entry.Timestamp;
                                cell.Style.NumberFormat.Format = "dd/mm/yyyy hh:mm:ss am/pm";
                                continue;
                            }
                            if (fieldId == "System_Duration")
                            {
                                // Excel almacena el tiempo como una fracción del día (1 día = 1440 min)
                                cell.Value = entry.InternalDurationMinutes / 1440.0;
                                cell.Style.NumberFormat.Format = "[mm]:ss";
                                continue;
                            }

                            // Campos dinámicos
                            if (entry.Values.TryGetValue(fieldId, out var val))
                            {
                                string strVal = val?.ToString() ?? "";

                                if (type == FieldType.Boolean)
                                {
                                    bool? valResult = null;
                                    if (val is bool b) valResult = b;
                                    else if (strVal == "1" || strVal.Equals("True", StringComparison.OrdinalIgnoreCase)) valResult = true;
                                    else if (strVal == "0" || strVal.Equals("False", StringComparison.OrdinalIgnoreCase)) valResult = false;

                                    if (valResult.HasValue)
                                    {
                                        cell.Value = valResult.Value ? 1 : 0;
                                        cell.Style.NumberFormat.Format = "0";
                                    }
                                    else cell.Value = strVal;
                                }
                                else if (type == FieldType.Numeric)
                                {
                                    if (double.TryParse(strVal, out double numVal))
                                    {
                                        cell.Value = numVal;
                                        string excelFormat = (def?.ShowDecimals ?? true) ? "0.00" : "0";
                                        cell.Style.NumberFormat.Format = excelFormat;
                                    }
                                    else cell.Value = strVal;
                                }
                                else if (type == FieldType.Calculation || type == FieldType.Average)
                                {
                                    if (strVal.Contains("%"))
                                    {
                                        string cleanVal = strVal.Replace("%", "").Trim();
                                        if (double.TryParse(cleanVal, out double pctVal))
                                        {
                                            cell.Value = pctVal / 100.0;
                                            string excelFormat = (def?.ShowDecimals ?? true) ? "0.0%" : "0%";
                                            cell.Style.NumberFormat.Format = excelFormat;
                                        }
                                        else cell.Value = strVal;
                                    }
                                    else if (double.TryParse(strVal, out double numVal))
                                    {
                                        cell.Value = numVal;
                                        cell.Style.NumberFormat.Format = "0.0%";
                                    }
                                    else cell.Value = strVal;
                                }
                                else if (type == FieldType.Date)
                                {
                                    if (DateTime.TryParse(strVal, out DateTime dateVal))
                                    {
                                        cell.Value = dateVal;
                                        cell.Style.NumberFormat.Format = "dd/mm/yyyy";
                                    }
                                    else cell.Value = strVal;
                                }
                                else if (type == FieldType.Time)
                                {
                                    if (DateTime.TryParse(strVal, out DateTime timeVal))
                                    {
                                        cell.Value = timeVal.TimeOfDay;
                                        cell.Style.NumberFormat.Format = def?.TimeFormat ?? "hh:mm:ss am/pm";
                                    }
                                    else cell.Value = strVal;
                                }
                                else
                                {
                                    cell.Value = strVal;
                                }

                                if (type == FieldType.TextArea || strVal.Contains("\n"))
                                {
                                    cell.Style.Alignment.SetWrapText(true);
                                }
                            }
                        }
                        row++;
                    }
                    worksheet.Columns().AdjustToContents();
                    foreach (var col in worksheet.Columns())
                    {
                        if (col.Width > 60) col.Width = 60;
                    }
                    workbook.SaveAs(filePath);
                }
                if (!silent) MessageBox.Show($"Exportación a Excel exitosa en:\n{filePath}");
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

                if (MessageBox.Show($"Exportación a JSON exitosa.\n\nArchivo guardado en:\n{filePath}\n\n¿Desea abrir la carpeta ahora?", "Éxito", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    if (Directory.Exists(exportDir)) System.Diagnostics.Process.Start("explorer.exe", exportDir);
                }
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
                        var headerRow = worksheet.Row(1);
                        var headers = headerRow.CellsUsed().ToDictionary(c => c.Address.ColumnNumber, c => c.Value.ToString().Trim());

                        // --- MAPEO DE COLUMNAS A IDs ---
                        var columnMap = new Dictionary<int, string>(); // ColumnIndex -> FieldId
                        int timestampColIndex = -1;
                        int durationColIndex = -1;

                        var exportConfig = CurrentPauta?.ExportConfig ?? new List<ExportColumnConfig>();

                        foreach (var h in headers)
                        {
                            string headerText = h.Value;

                            // 1. Checar campos sistema
                            if (headerText.Equals("Fecha de evaluación", StringComparison.OrdinalIgnoreCase)) { timestampColIndex = h.Key; continue; }
                            if (headerText.Equals("Duración (min)", StringComparison.OrdinalIgnoreCase)) { durationColIndex = h.Key; continue; }

                            // 2. Checar configuración de exportación (Header Personalizado)
                            var configMatch = exportConfig.FirstOrDefault(c => string.Equals(c.CustomHeader, headerText, StringComparison.OrdinalIgnoreCase));
                            if (configMatch != null)
                            {
                                columnMap[h.Key] = configMatch.FieldId;
                                continue;
                            }

                            // 3. Checar Labels de campos actuales (Nombre original)
                            var fieldMatch = CurrentFields.FirstOrDefault(f => string.Equals(f.Label, headerText, StringComparison.OrdinalIgnoreCase));
                            if (fieldMatch != null)
                            {
                                columnMap[h.Key] = fieldMatch.Id;
                            }
                        }

                        if (columnMap.Count == 0 && timestampColIndex == -1)
                        {
                            MessageBox.Show("No se pudieron mapear las columnas. Asegúrate de que los nombres de cabecera coincidan con la Pauta actual.", "Error Importación", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        int importedCount = 0;
                        foreach (var row in rows)
                        {
                            var entry = new AuditEntry();

                            // Leer Timestamp
                            if (timestampColIndex != -1 && !row.Cell(timestampColIndex).IsEmpty())
                            {
                                string val = row.Cell(timestampColIndex).Value.ToString();
                                if (DateTime.TryParse(val, out DateTime dt)) entry.Timestamp = dt;
                            }

                            // Leer Duración
                            if (durationColIndex != -1 && !row.Cell(durationColIndex).IsEmpty())
                            {
                                string val = row.Cell(durationColIndex).Value.ToString();
                                if (double.TryParse(val, out double d)) entry.InternalDurationMinutes = d;
                            }

                            bool hasContent = false;
                            foreach (var kvp in columnMap)
                            {
                                int colIndex = kvp.Key;
                                string fieldId = kvp.Value;
                                var cell = row.Cell(colIndex);

                                if (cell.IsEmpty()) continue;

                                var fieldDef = CurrentFields.FirstOrDefault(f => f.Id == fieldId);
                                if (fieldDef == null) continue; // Campo ya no existe en pauta actual

                                string importedVal = cell.Value.ToString();

                                // --- NORMALIZACIÓN DE TIPOS ---
                                if (fieldDef.Type == FieldType.Date)
                                {
                                    if (DateTime.TryParse(importedVal, out DateTime dateVal))
                                        importedVal = dateVal.ToString("dd/MM/yyyy");
                                }
                                else if (fieldDef.Type == FieldType.Time)
                                {
                                    if (DateTime.TryParse(importedVal, out DateTime timeVal))
                                        importedVal = timeVal.ToString(fieldDef.Definition.TimeFormat ?? "HH:mm");
                                    else if (TimeSpan.TryParse(importedVal, out TimeSpan tsVal))
                                        importedVal = DateTime.Today.Add(tsVal).ToString(fieldDef.Definition.TimeFormat ?? "HH:mm");
                                }
                                else if (fieldDef.Type == FieldType.Calculation || fieldDef.Type == FieldType.Average)
                                {
                                    // Si Excel nos da 0.5, convertir a 50%
                                    // Si Excel nos da "50%", dejar como está
                                    if (!importedVal.Contains("%") && double.TryParse(importedVal, out double numVal))
                                    {
                                        if (numVal <= 1.0) numVal *= 100; // Asumir que < 1 es decimal (0.5 = 50%)
                                        importedVal = $"{Math.Round(numVal, 1)}%";
                                    }
                                }
                                else if (fieldDef.Type == FieldType.Boolean)
                                {
                                    if (importedVal == "1" || importedVal.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) importedVal = "True";
                                    else if (importedVal == "0" || importedVal.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) importedVal = "False";
                                }

                                entry.Values[fieldId] = importedVal;
                                hasContent = true;
                            }

                            if (hasContent || timestampColIndex != -1)
                            {
                                Records.Add(entry);
                                importedCount++;
                            }
                        }

                        if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
                        MessageBox.Show($"Importación completada. Se importaron {importedCount} registros.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
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
                var definitions = CurrentFields.Select(f => f.Definition).ToList();
                int count = 0;

                foreach (var record in records)
                {
                    string filename = GetPdfFileName(record);
                    string fullPath = Path.Combine(folderPath, filename);

                    // Si el archivo ya existe (ej: mismo ticket/analista en pocos segundos), agregamos un sufijo
                    if (File.Exists(fullPath))
                    {
                        string nameOnly = Path.GetFileNameWithoutExtension(filename);
                        fullPath = Path.Combine(folderPath, $"{nameOnly}_{count + 1}.pdf");
                    }

                    _pdfService.GenerateAuditPdf(new List<AuditEntry> { record }, definitions, CurrentPauta?.PdfConfig, pautaName, fullPath);
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

        private string GetPdfFileName(AuditEntry record)
        {
            string pautaName = CurrentPauta?.Name ?? "Auditoria";
            string safePautaName = string.Join("_", pautaName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
            string timestamp = record.Timestamp.ToString("yyyyMMdd_HHmmss");

            if (CurrentPauta != null)
            {
                var parts = new List<string>();
                parts.Add("Reporte");
                parts.Add(safePautaName);

                bool hasCustomFields = false;

                // Intentar obtener valor del Campo 1 (ej. Analista)
                if (!string.IsNullOrEmpty(CurrentPauta.PdfFileNameFieldId1) && record.Values.TryGetValue(CurrentPauta.PdfFileNameFieldId1, out var val1) && val1 != null)
                {
                    string s1 = string.Join("_", val1.ToString()?.Split(Path.GetInvalidFileNameChars()) ?? Array.Empty<string>()).Trim().Replace(" ", "_");
                    if (!string.IsNullOrEmpty(s1))
                    {
                        parts.Add(s1);
                        hasCustomFields = true;
                    }
                }

                // Intentar obtener valor del Campo 2 (ej. Ticket)
                if (!string.IsNullOrEmpty(CurrentPauta.PdfFileNameFieldId2) && record.Values.TryGetValue(CurrentPauta.PdfFileNameFieldId2, out var val2) && val2 != null)
                {
                    string s2 = string.Join("_", val2.ToString()?.Split(Path.GetInvalidFileNameChars()) ?? Array.Empty<string>()).Trim().Replace(" ", "_");
                    if (!string.IsNullOrEmpty(s2))
                    {
                        parts.Add(s2);
                        hasCustomFields = true;
                    }
                }

                if (hasCustomFields)
                {
                    parts.Add(timestamp);
                    return string.Join("_", parts) + ".pdf";
                }
            }

            // Fallback al nombre por defecto si no hay campos configurados o están vacíos
            return $"Reporte_{safePautaName}_{timestamp}.pdf";
        }

        private string? GeneratePdfCommon(List<AuditEntry> records, bool silent = false)
        {
            try
            {
                string pautaName = CurrentPauta?.Name ?? "Auditoria";
                var definitions = CurrentFields.Select(f => f.Definition).ToList();
                var settings = _storageService.LoadSettings();
                string exportDir = settings.PdfReportPath;
                if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

                string fileName;
                if (records.Count == 1)
                {
                    fileName = GetPdfFileName(records[0]);
                }
                else
                {
                    fileName = $"Reporte_Multiple_{pautaName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                }

                string filePath = Path.Combine(exportDir, fileName);

                _pdfService.GenerateAuditPdf(records, definitions, CurrentPauta?.PdfConfig, pautaName, filePath);

                if (!silent)
                {
                    if (MessageBox.Show($"PDF Generado con éxito en:\n{filePath}\n\n¿Abrir ahora?", "Éxito", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true } }.Start();
                    }
                }
                return filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar PDF: {ex.Message}");
                return null;
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

        // Duplicate command properties removed.

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
            Records.CollectionChanged += (s, e) => UpdateAuditStats();
            UpdateAuditStats();

            ApplyRowColoring();
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
                    var def = calcField.Definition;
                    var rules = def.ScoringRules;

                    // 1.1 Anulación Crítica (Zero Trigger)
                    if (def.EnableZeroTrigger)
                    {
                        var triggerIds = def.ZeroTriggerFieldIds ?? new List<string>();
                        if (!triggerIds.Any() && !string.IsNullOrEmpty(def.ZeroTriggerFieldId))
                            triggerIds = new List<string> { def.ZeroTriggerFieldId };

                        if (triggerIds.Any())
                        {
                            bool triggered = false;
                            foreach (var fid in triggerIds)
                            {
                                var triggerSource = CurrentFields.FirstOrDefault(f => f.Id == fid);
                                if (triggerSource != null)
                                {
                                    string triggerVal = triggerSource.Value?.ToString() ?? "";
                                    if (triggerSource.Type == FieldType.Boolean)
                                    {
                                        if (triggerSource.Value is bool b) triggerVal = b ? "1" : "0";
                                    }

                                    if (string.Equals(triggerVal, def.ZeroTriggerValue, StringComparison.OrdinalIgnoreCase))
                                    {
                                        triggered = true;
                                        break;
                                    }
                                }
                            }

                            if (triggered)
                            {
                                calcField.Value = def.ShowDecimals ? "0.0%" : "0%";
                                continue;
                            }
                        }
                    }

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
                            // Si el campo es Boolean, normalizamos el valor actual a "1"/"0" para comparar con los mapeos
                            string normalizedVal = currentVal;
                            if (source.Type == FieldType.Boolean)
                            {
                                if (source.Value is bool b) normalizedVal = b ? "1" : "0";
                                else if (currentVal.Equals("True", StringComparison.OrdinalIgnoreCase)) normalizedVal = "1";
                                else if (currentVal.Equals("False", StringComparison.OrdinalIgnoreCase)) normalizedVal = "0";
                            }

                            var mapping = rule.Mappings.FirstOrDefault(m =>
                                m.Value.Equals(normalizedVal, StringComparison.OrdinalIgnoreCase) ||
                                (m.Value.Contains(" ") && m.Value.Split(' ')[0].Equals(normalizedVal, StringComparison.OrdinalIgnoreCase))
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

                    double percentage = totalPossibleWeights > 0 ? (totalEarnedWeights / totalPossibleWeights * 100) : 0;

                    // Aplicar redondeo si es necesario
                    if (def.Rounding == CalculationRounding.Up) percentage = Math.Ceiling(percentage);
                    else if (def.Rounding == CalculationRounding.Down) percentage = Math.Floor(percentage);

                    string format = def.ShowDecimals ? "F1" : "F0";
                    calcField.Value = $"{percentage.ToString(format)}%";
                }

                // 2. CÁLCULO DE PROMEDIOS (AVERAGE)
                foreach (var avgField in CurrentFields.Where(f => f.Type == FieldType.Average))
                {
                    var def = avgField.Definition;

                    // 2.1 Anulación Crítica (Zero Trigger)
                    if (def.EnableZeroTrigger)
                    {
                        var triggerIds = def.ZeroTriggerFieldIds ?? new List<string>();
                        if (!triggerIds.Any() && !string.IsNullOrEmpty(def.ZeroTriggerFieldId))
                            triggerIds = new List<string> { def.ZeroTriggerFieldId };

                        if (triggerIds.Any())
                        {
                            bool triggered = false;
                            foreach (var fid in triggerIds)
                            {
                                var triggerSource = CurrentFields.FirstOrDefault(f => f.Id == fid);
                                if (triggerSource != null)
                                {
                                    string triggerVal = triggerSource.Value?.ToString() ?? "";
                                    if (triggerSource.Type == FieldType.Boolean)
                                    {
                                        if (triggerSource.Value is bool b) triggerVal = b ? "1" : "0";
                                    }

                                    if (string.Equals(triggerVal, def.ZeroTriggerValue, StringComparison.OrdinalIgnoreCase))
                                    {
                                        triggered = true;
                                        break;
                                    }
                                }
                            }

                            if (triggered)
                            {
                                avgField.Value = def.ShowDecimals ? "0.0%" : "0%";
                                continue;
                            }
                        }
                    }

                    var targets = CurrentFields.Where(f => def.TargetIds.Contains(f.Id)).ToList();
                    double sum = 0;
                    int count = 0;
                    foreach (var t in targets)
                    {
                        string valText = t.Value?.ToString()?.Replace("%", "") ?? "";
                        if (double.TryParse(valText, out double d)) { sum += d; count++; }
                    }

                    double avg = count > 0 ? (sum / count) : 0;

                    // Redondeo de promedio
                    if (def.Rounding == CalculationRounding.Up) avg = Math.Ceiling(avg);
                    else if (def.Rounding == CalculationRounding.Down) avg = Math.Floor(avg);

                    string format = def.ShowDecimals ? "F1" : "F0";
                    avgField.Value = $"{avg.ToString(format)}%";
                }

                // 3. AUTO-SELECCIÓN (BASADA EN REGLAS)
                ApplyAutoSelectRules();
            }
            finally { _isCalculating = false; }
        }

        private void ApplyAutoSelectRules()
        {
            foreach (var field in CurrentFields)
            {
                if (field.Definition.AutoSelectRules == null || !field.Definition.AutoSelectRules.Any()) continue;

                foreach (var rule in field.Definition.AutoSelectRules)
                {
                    if (string.IsNullOrEmpty(rule.SourceFieldId)) continue;
                    var source = CurrentFields.FirstOrDefault(f => f.Id == rule.SourceFieldId);
                    if (source == null) continue;

                    string sourceVal = source.Value?.ToString() ?? "";
                    if (EvaluateRule(sourceVal, rule.Operator, rule.Value))
                    {
                        field.Value = rule.TargetValue;
                        break; // Primera regla que cumple gana
                    }
                }
            }
        }

        private bool EvaluateRule(string sourceVal, string op, string ruleVal)
        {
            if (string.IsNullOrEmpty(sourceVal) || string.IsNullOrEmpty(ruleVal)) return false;

            // Limpieza básica para porcentajes y espacios
            string s = sourceVal.Replace("%", "").Trim();
            string r = ruleVal.Replace("%", "").Trim();

            if (op == "=") return string.Equals(s, r, StringComparison.OrdinalIgnoreCase);

            if (double.TryParse(s, out double sNum) && double.TryParse(r, out double rNum))
            {
                switch (op)
                {
                    case ">": return sNum > rNum;
                    case "<": return sNum < rNum;
                    case ">=": return sNum >= rNum;
                    case "<=": return sNum <= rNum;
                }
            }
            return false;
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
            _currentAuditStartTime = DateTime.Now; // Reiniciar contador para nuevo registro
            OnPropertyChanged(nameof(IsEditMode));
        }

        private void LoadRecordToForm(AuditEntry? record)
        {
            if (record == null) return;
            foreach (var field in CurrentFields)
            {
                if (record.Values.TryGetValue(field.Id, out var value))
                {
                    // FIX: Desempaquetar JsonElement para evitar errores de binding (especialmente en CheckBox)
                    if (value is System.Text.Json.JsonElement element)
                    {
                        switch (element.ValueKind)
                        {
                            case System.Text.Json.JsonValueKind.True: field.Value = true; break;
                            case System.Text.Json.JsonValueKind.False: field.Value = false; break;
                            case System.Text.Json.JsonValueKind.String: field.Value = element.GetString(); break;
                            case System.Text.Json.JsonValueKind.Number:
                                if (element.TryGetDouble(out double d)) field.Value = d;
                                else field.Value = element.ToString();
                                break;
                            default: field.Value = element.ToString(); break;
                        }
                    }
                    else
                    {
                        field.Value = value;
                    }
                }
                else
                {
                    field.Value = null;
                }
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

            if (SelectedRecord == null)
            {
                Records.Add(entry);

                // Calcular duración solo para nuevos registros
                var settings = _storageService.LoadSettings();
                if (settings.EnableInternalTimer)
                {
                    entry.InternalDurationMinutes = (DateTime.Now - _currentAuditStartTime).TotalMinutes;
                }
            }
            entry.NotifyUpdate();
            if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
            ApplyRowColoring();

            // Reiniciar todo para la siguiente auditoría (limpia campos y resetea el temporizador)
            CreateNewRecord();

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

        private void SendEmails(AuditEntry? singleEntry = null)
        {
            if (CurrentPauta == null)
            {
                MessageBox.Show("No hay una pauta activa.");
                return;
            }

            // Determinar registros a procesar
            List<AuditEntry> toProcess = new();
            if (singleEntry != null)
            {
                toProcess.Add(singleEntry);
            }
            else
            {
                var selected = Records.Where(r => r.IsSelected).ToList();
                toProcess = selected.Any() ? selected : Records.ToList();
            }

            if (!toProcess.Any())
            {
                MessageBox.Show("No hay registros para enviar.");
                return;
            }

            // Aplicar lógica de exclusión si estamos enviando múltiples
            if (toProcess.Count > 1 && !string.IsNullOrEmpty(CurrentPauta.ExcludeByFieldId))
            {
                int totalBefore = toProcess.Count;
                toProcess = toProcess.Where(entry =>
                {
                    if (entry.Values.TryGetValue(CurrentPauta.ExcludeByFieldId, out var val))
                    {
                        string valStr = val?.ToString() ?? "";
                        return !string.Equals(valStr, CurrentPauta.ExcludeByFieldValue, StringComparison.OrdinalIgnoreCase);
                    }
                    return true;
                }).ToList();

                int excluded = totalBefore - toProcess.Count;
                if (excluded > 0)
                {
                    var res = MessageBox.Show($"Se han excluido {excluded} registros según la regla de la pauta.\n\n¿Desea continuar con los {toProcess.Count} restantes?", "Filtro de Exclusión", MessageBoxButton.YesNo);
                    if (res == MessageBoxResult.No) return;
                }
            }

            if (toProcess.Count > 1)
            {
                var confirm = MessageBox.Show($"Se prepararán {toProcess.Count} correos individuales. ¿Continuar?", "Confirmar Envío", MessageBoxButton.YesNo);
                if (confirm == MessageBoxResult.No) return;
            }

            var globalSettings = _storageService.LoadSettings();
            var fieldDefinitions = _storageService.LoadConfiguration(CurrentPauta.Id);
            int count = 0;
            var generatedPdfs = new List<string>();

            foreach (var entry in toProcess)
            {
                try
                {
                    // Generar PDF individual (para el adjunto)
                    string? pdfPath = GeneratePdfCommon(new List<AuditEntry> { entry }, silent: true);
                    if (!string.IsNullOrEmpty(pdfPath)) generatedPdfs.Add(pdfPath);

                    // El EmailService ahora maneja la herencia internamente
                    _emailService.SendEmail(globalSettings, CurrentPauta, entry, fieldDefinitions, pdfPath);
                    count++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al procesar registro: {ex.Message}");
                }
            }

            if (count > 0)
            {
                string msg = $"{count} correos procesados.";

                // Determinar el método efectivo para el aviso de adjuntos
                EmailMethod effectiveMethod = CurrentPauta.EmailMethod;

                if (generatedPdfs.Any())
                {
                    msg += $"\n\nLos reportes PDF se guardaron en:\n{globalSettings.PdfReportPath}";
                    if (effectiveMethod == EmailMethod.Mailto)
                    {
                        msg += "\n\n⚠️ NOTA: El método 'Mailto' NO permite adjuntar archivos automáticamente. Deberá adjuntar los PDFs manualmente en cada correo.";
                    }
                }

                if (MessageBox.Show(msg + "\n\n¿Desea abrir la carpeta de los reportes ahora?", "Proceso Finalizado", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    if (Directory.Exists(globalSettings.PdfReportPath))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", globalSettings.PdfReportPath);
                    }
                }
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
                if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
                CreateNewRecord();
            }
        }

        // End of MainViewModel
    }
}
