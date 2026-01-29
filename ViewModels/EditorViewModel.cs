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
using ClosedXML.Excel;
using System.Collections.Generic;

namespace PautaDinamicaApp.ViewModels
{
    public class EditorViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;
        private ObservableCollection<FieldDefinition> _fields;
        private string _initialFieldsJson = string.Empty;
        private string _activePautaIdInMain; // Track which pauta is currently open in Main window

        private ObservableCollection<PautaSchema> _pautas = new();
        private PautaSchema? _editingPauta;
        private bool _isPautaMultiSelectMode;
        private bool _isMultiSelectMode;
        private readonly List<PautaSchema> _pautasToDelete = new();

        public EditorViewModel(string activePautaId = "")
        {
            _activePautaIdInMain = activePautaId;
            _storageService = new StorageService();
            _fields = new ObservableCollection<FieldDefinition>();
            _initialFieldsJson = "[]";

            LoadPautaList();

            // Comandos de Campos
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
            PickDateCommand = new RelayCommand(p => PickDate(p as FieldDefinition));
            PickTimeCommand = new RelayCommand(p => PickTime(p as FieldDefinition));

            // Comandos de Gestión de Pautas
            AddPautaCommand = new RelayCommand(_ => AddPauta());
            DeletePautaCommand = new RelayCommand(p => DeletePauta(p as PautaSchema));
            DeleteSelectedPautasCommand = new RelayCommand(_ => DeleteSelectedPautas());
            TogglePautaMultiSelectCommand = new RelayCommand(_ => IsPautaMultiSelectMode = !IsPautaMultiSelectMode);
            SelectAllPautasCommand = new RelayCommand(_ => SelectAllPautas());
            ExportAllDatabaseCommand = new RelayCommand(_ => ExportAllDatabase());
            ImportAllDatabaseCommand = new RelayCommand(_ => ImportAllDatabase());

            AvailableTypes = Enum.GetValues(typeof(FieldType)).Cast<FieldType>()
                                .Where(t => t != FieldType.Separator)
                                .ToList();
        }

        public bool IsSaveSuccessful { get; private set; }
        public bool ShouldClearRecords { get; private set; }
        public ICommand PickDateCommand { get; }
        public ICommand PickTimeCommand { get; }

        public ICommand AddPautaCommand { get; }
        public ICommand DeletePautaCommand { get; }
        public ICommand DeleteSelectedPautasCommand { get; }
        public ICommand TogglePautaMultiSelectCommand { get; }
        public ICommand SelectAllPautasCommand { get; }
        public ICommand ExportAllDatabaseCommand { get; }
        public ICommand ImportAllDatabaseCommand { get; }

        public ObservableCollection<PautaSchema> Pautas
        {
            get => _pautas;
            set => SetProperty(ref _pautas, value);
        }

        public PautaSchema? EditingPauta
        {
            get => _editingPauta;
            set
            {
                // Antes de cambiar, podrías advertir si hay cambios sin guardar, 
                // pero por ahora solo cargamos la nueva pauta.
                if (SetProperty(ref _editingPauta, value) && value != null)
                {
                    LoadPautaFields(value.Id);
                }
            }
        }

        public bool IsPautaMultiSelectMode
        {
            get => _isPautaMultiSelectMode;
            set
            {
                if (SetProperty(ref _isPautaMultiSelectMode, value) && !value)
                {
                    foreach (var p in Pautas) p.IsSelected = false;
                }
            }
        }

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

        public List<FieldType> AvailableTypes { get; }

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

        public bool HasPendingChanges()
        {
            if (EditingPauta != null)
            {
                string currentFieldsJson = JsonSerializer.Serialize(Fields);
                if (_initialFieldsJson != currentFieldsJson) return true;
            }

            var savedPautas = _storageService.LoadPautas();
            if (savedPautas.Count != Pautas.Count) return true;
            if (_pautasToDelete.Any()) return true;

            for (int i = 0; i < Pautas.Count; i++)
            {
                if (Pautas[i].Id != savedPautas[i].Id || Pautas[i].Name != savedPautas[i].Name)
                    return true;
            }

            return false;
        }

        private void LoadPautaList()
        {
            var list = _storageService.LoadPautas();
            Pautas = new ObservableCollection<PautaSchema>(list);

            string lastId = string.IsNullOrEmpty(_activePautaIdInMain) ? _storageService.GetLastPautaId() : _activePautaIdInMain;
            EditingPauta = Pautas.FirstOrDefault(p => p.Id == lastId) ?? Pautas.FirstOrDefault();
        }

        private void LoadPautaFields(string pautaId)
        {
            var config = _storageService.LoadConfiguration(pautaId).OrderBy(f => f.Order).ToList();
            foreach (var f in config) f.EnsureDefaultOptions();
            Fields = new ObservableCollection<FieldDefinition>(config);
            _initialFieldsJson = JsonSerializer.Serialize(Fields);
        }

        private void AddField()
        {
            var lastField = Fields.OrderBy(f => f.Order).LastOrDefault();
            Fields.Add(new FieldDefinition
            {
                Id = "f_" + Guid.NewGuid().ToString().Substring(0, 4),
                Label = GetNextAvailableLabel("Nuevo Campo"),
                Category = lastField?.Category ?? "General",
                Type = FieldType.Text,
                Order = (lastField?.Order ?? 0) + 1,
                MaxLength = 255
            });
        }

        private void AddSection()
        {
            var lastField = Fields.OrderBy(f => f.Order).LastOrDefault();
            Fields.Add(new FieldDefinition
            {
                Id = "s_" + Guid.NewGuid().ToString().Substring(0, 4),
                Label = GetNextAvailableLabel("Nuevo Cuadro"),
                Category = "--- SECCIÓN ---",
                Type = FieldType.Separator,
                Order = (lastField?.Order ?? 0) + 1
            });
        }

        private string GetNextAvailableLabel(string baseName)
        {
            var existingLabels = Fields.Select(f => f.Label).ToList();
            if (!existingLabels.Contains(baseName)) return baseName;
            int i = 2;
            while (true)
            {
                string candidate = $"{baseName} {i}";
                if (!existingLabels.Contains(candidate)) return candidate;
                i++;
            }
        }

        private void RemoveField(FieldDefinition? field)
        {
            if (field != null && MessageBox.Show($"¿Eliminar campo [{field.Label}]?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                Fields.Remove(field);
        }

        private void MoveUp(FieldDefinition? field)
        {
            if (field == null) return;
            int index = Fields.IndexOf(field);
            if (index > 0) Fields.Move(index, index - 1);
        }

        private void MoveDown(FieldDefinition? field)
        {
            if (field == null) return;
            int index = Fields.IndexOf(field);
            if (index < Fields.Count - 1) Fields.Move(index, index + 1);
        }

        private void ConfigureOptions(FieldDefinition? field)
        {
            if (field == null) return;
            var configurableTypes = new[] { FieldType.Dropdown, FieldType.Boolean, FieldType.Calculation, FieldType.Average, FieldType.Time, FieldType.Text, FieldType.TextArea, FieldType.Numeric };
            if (!configurableTypes.Contains(field.Type)) return;

            var vm = new OptionsEditorViewModel(field, Fields.ToList());
            var win = new OptionsWindow { DataContext = vm, Owner = Application.Current.MainWindow };
            if (win.ShowDialog() == true)
            {
                field.UseCustomWeights = vm.UseCustomWeights;
                field.MaxLength = vm.ResultMaxLength;
                field.WarnOnDuplicate = vm.WarnOnDuplicate;
                field.TimeFormat = vm.TimeFormat;
                if (field.Type == FieldType.Dropdown) field.Options = vm.ResultOptions;
                else if (field.Type == FieldType.Calculation) field.ScoringRules = vm.ResultRules;
                else if (field.Type == FieldType.Average) field.TargetIds = vm.ResultAverageIds;
                OnPropertyChanged(nameof(Fields));
            }
        }

        private void PickDate(FieldDefinition? field)
        {
            if (field == null) return;
            var selector = new DateSelectorWindow(field.DefaultValue) { Owner = Application.Current.MainWindow };
            if (selector.ShowDialog() == true)
            {
                field.DefaultValue = selector.SelectedValue == "TODAY" ? DateTime.Now.ToString("dd/MM/yyyy") : selector.SelectedValue;
            }
        }

        private void PickTime(FieldDefinition? field)
        {
            if (field == null) return;
            var selector = new TimeSelectorWindow(field.DefaultValue, field.TimeFormat) { Owner = Application.Current.MainWindow };
            if (selector.ShowDialog() == true)
            {
                field.DefaultValue = selector.SelectedValue == "NOW" ? DateTime.Now.ToString(field.TimeFormat ?? "HH:mm") : selector.SelectedValue;
            }
        }

        private void ExportConfig()
        {
            var sfd = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", FileName = $"Config_{EditingPauta?.Name}_{DateTime.Now:yyyyMMdd}.json" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string json = JsonSerializer.Serialize(Fields, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(sfd.FileName, json);
                    MessageBox.Show("Configuración exportada.");
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void ImportConfig()
        {
            var ofd = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json" };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var imported = JsonSerializer.Deserialize<ObservableCollection<FieldDefinition>>(json);
                    if (imported != null && MessageBox.Show("¿Reemplazar diseño actual?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        Fields = imported;
                        foreach (var f in Fields) f.EnsureDefaultOptions();
                    }
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void ToggleMultiSelect()
        {
            IsMultiSelectMode = !IsMultiSelectMode;
            if (!IsMultiSelectMode) foreach (var f in Fields) f.IsSelected = false;
        }

        private void SelectAll()
        {
            bool all = Fields.All(f => f.IsSelected);
            foreach (var f in Fields) f.IsSelected = !all;
            OnPropertyChanged(nameof(Fields));
        }

        private void DeleteSelected()
        {
            var selected = Fields.Where(f => f.IsSelected).ToList();
            if (selected.Any() && MessageBox.Show($"¿Eliminar {selected.Count} campos?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var f in selected) Fields.Remove(f);
            }
        }

        private void AddPauta()
        {
            var newPauta = new PautaSchema { Name = "Nueva Pauta " + (Pautas.Count + 1) };
            Pautas.Add(newPauta);
            EditingPauta = newPauta;
        }

        private void DeletePauta(PautaSchema? p)
        {
            if (p == null) return;
            if (Pautas.Count <= 1) { MessageBox.Show("Debe quedar una pauta."); return; }
            if (MessageBox.Show($"¿Borrar '{p.Name}' al guardar?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Pautas.Remove(p);
                if (!_pautasToDelete.Contains(p)) _pautasToDelete.Add(p);
                if (EditingPauta == p) EditingPauta = Pautas.First();
            }
        }

        private void DeleteSelectedPautas()
        {
            var selected = Pautas.Where(p => p.IsSelected).ToList();
            if (!selected.Any()) return;
            if (selected.Count >= Pautas.Count) { MessageBox.Show("No puedes borrar todas."); return; }
            if (MessageBox.Show($"¿Borrar {selected.Count} pautas al guardar?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var p in selected)
                {
                    Pautas.Remove(p);
                    if (!_pautasToDelete.Contains(p)) _pautasToDelete.Add(p);
                }
                if (EditingPauta == null || !Pautas.Contains(EditingPauta)) EditingPauta = Pautas.First();
            }
        }

        private void SelectAllPautas()
        {
            bool all = Pautas.All(p => p.IsSelected);
            foreach (var p in Pautas) p.IsSelected = !all;
        }

        private void ExportAllDatabase()
        {
            var sfd = new SaveFileDialog { Filter = "Database JSON (*.json)|*.json", FileName = $"System_Backup_{DateTime.Now:yyyyMMdd}.json" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var fullData = new { Pautas = Pautas.ToList(), Configs = Pautas.ToDictionary(p => p.Id, p => _storageService.LoadConfiguration(p.Id)), Records = Pautas.ToDictionary(p => p.Id, p => _storageService.LoadRecords(p.Id)) };
                    File.WriteAllText(sfd.FileName, JsonSerializer.Serialize(fullData, new JsonSerializerOptions { WriteIndented = true }));
                    MessageBox.Show("Respaldo completo exportado.");
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void ImportAllDatabase()
        {
            var ofd = new OpenFileDialog { Filter = "Database JSON (*.json)|*.json" };
            if (ofd.ShowDialog() == true && MessageBox.Show("¿Reemplazar TODO el sistema?", "Atención", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    var doc = JsonDocument.Parse(File.ReadAllText(ofd.FileName));
                    var pautas = JsonSerializer.Deserialize<List<PautaSchema>>(doc.RootElement.GetProperty("Pautas").GetRawText());
                    var configs = JsonSerializer.Deserialize<Dictionary<string, List<FieldDefinition>>>(doc.RootElement.GetProperty("Configs").GetRawText());
                    if (pautas != null && configs != null)
                    {
                        foreach (var p in _storageService.LoadPautas()) _storageService.DeletePautaFiles(p.Id);
                        _storageService.SavePautas(pautas);
                        foreach (var kvp in configs) _storageService.SaveConfiguration(kvp.Key, kvp.Value);
                        if (doc.RootElement.TryGetProperty("Records", out var recsProp))
                        {
                            var records = JsonSerializer.Deserialize<Dictionary<string, List<AuditEntry>>>(recsProp.GetRawText());
                            if (records != null) foreach (var kvp in records) _storageService.SaveRecords(kvp.Key, kvp.Value);
                        }
                        LoadPautaList();
                        MessageBox.Show("Base de datos restaurada.");
                    }
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void SaveConfig()
        {
            IsSaveSuccessful = false;
            ShouldClearRecords = false;

            // 1. Validaciones
            if (Fields.Any(f => string.IsNullOrWhiteSpace(f.Label)))
            {
                MessageBox.Show("Hay campos sin nombre.");
                return;
            }
            if (Fields.GroupBy(f => f.Label.Trim().ToLower()).Any(g => g.Count() > 1))
            {
                MessageBox.Show("Hay nombres duplicados.");
                return;
            }

            // 2. Procesar estructura para la pauta actual
            var list = Fields.ToList();
            string currentBox = "General";
            for (int i = 0; i < list.Count; i++)
            {
                list[i].Order = i;
                if (list[i].Type == FieldType.Separator) { currentBox = list[i].Label; list[i].Category = "--- SECCIÓN ---"; }
                else { list[i].Category = currentBox; list[i].EnsureDefaultOptions(); }
            }

            // 3. Detectar cambios en pauta actual
            if (EditingPauta != null)
            {
                string currentFieldsJson = JsonSerializer.Serialize(Fields);
                bool hasStructuralChanges = _initialFieldsJson != currentFieldsJson;

                if (hasStructuralChanges)
                {
                    var records = _storageService.LoadRecords(EditingPauta.Id);
                    if (records.Any())
                    {
                        var res = MessageBox.Show($"La pauta '{EditingPauta.Name}' tiene {records.Count} registros.\n¿Reestrellar base de datos y respaldar a Excel?", "Cambio Estructural", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                        if (res == MessageBoxResult.Cancel) return;
                        if (res == MessageBoxResult.Yes)
                        {
                            var sfd = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = $"Resp_{EditingPauta.Name}_{DateTime.Now:yyyyMMdd}.xlsx" };
                            if (sfd.ShowDialog() == true)
                            {
                                try
                                {
                                    // Backup usando config ANTERIOR (la de disco)
                                    var oldConfig = _storageService.LoadConfiguration(EditingPauta.Id);
                                    ExportToExcelInternal(sfd.FileName, records, oldConfig);

                                    // Limpiar registros en disco
                                    _storageService.SaveRecords(EditingPauta.Id, new List<AuditEntry>());

                                    // Si es la pauta activa en Main, avisar para limpiar UI
                                    if (EditingPauta.Id == _activePautaIdInMain) ShouldClearRecords = true;
                                }
                                catch (Exception ex) { MessageBox.Show("Error respaldo: " + ex.Message); return; }
                            }
                            else return;
                        }
                    }
                    _storageService.BackupConfiguration(EditingPauta.Id);
                    _storageService.SaveConfiguration(EditingPauta.Id, list);
                    _initialFieldsJson = currentFieldsJson;
                }
            }

            // 4. Procesar eliminaciones
            foreach (var p in _pautasToDelete)
            {
                var records = _storageService.LoadRecords(p.Id);
                if (records.Any() && MessageBox.Show($"La pauta '{p.Name}' tiene registros. ¿Respaldar a Excel antes de borrar?", "Eliminación", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var sfd = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = $"Final_{p.Name}.xlsx" };
                    if (sfd.ShowDialog() == true)
                    {
                        try { ExportToExcelInternal(sfd.FileName, records, _storageService.LoadConfiguration(p.Id)); } catch { }
                    }
                }

                // Solo pedir backup JSON si el archivo EXISTE (no es una pauta nueva sin guardar)
                if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data", $"pauta_{p.Id}_config.json")))
                {
                    if (MessageBox.Show($"¿Respaldar JSON de '{p.Name}' antes de borrar?", "Borrar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        var sfd = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = $"Backup_{p.Name}.json" };
                        if (sfd.ShowDialog() == true) File.WriteAllText(sfd.FileName, JsonSerializer.Serialize(_storageService.LoadConfiguration(p.Id)));
                    }
                }

                _storageService.DeletePautaFiles(p.Id);
            }
            _pautasToDelete.Clear();

            // 5. Guardar índice
            _storageService.SavePautas(Pautas.ToList());
            IsSaveSuccessful = true;
            MessageBox.Show("Cambios guardados con éxito.");
        }

        private void ExportToExcelInternal(string filePath, List<AuditEntry> records, List<FieldDefinition> config)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Auditoría");
                ws.Cell(1, 1).Value = "Fecha";
                var fields = config.OrderBy(f => f.Order).ToList();
                for (int i = 0; i < fields.Count; i++) ws.Cell(1, i + 2).Value = fields[i].Label;

                int row = 2;
                foreach (var entry in records)
                {
                    ws.Cell(row, 1).Value = entry.Timestamp.ToString("g");
                    for (int i = 0; i < fields.Count; i++)
                    {
                        if (entry.Values.TryGetValue(fields[i].Id, out var val))
                            ws.Cell(row, i + 2).Value = val?.ToString() ?? "";
                    }
                    row++;
                }
                ws.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }
    }
}
