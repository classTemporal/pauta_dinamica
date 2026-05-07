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
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;

namespace PautaDinamicaApp.ViewModels
{
    public class EditorViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;
        private ObservableCollection<FieldDefinition> _fields;
        private string _initialFieldsJson = string.Empty;
        private List<string> _initialFieldIds = new();
        private string _activePautaIdInMain; // Track which pauta is currently open in Main window

        private ObservableCollection<PautaSchema> _pautas = new();
        private PautaSchema? _editingPauta;
        private bool _isPautaMultiSelectMode;
        private bool _isMultiSelectMode;
        private readonly List<PautaSchema> _pautasToDelete = new();
        private readonly Dictionary<string, List<FieldDefinition>> _unsavedConfigs = new();
        private readonly Dictionary<string, List<AuditEntry>> _unsavedRecords = new();
        private int _selectedTabIndex;

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

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

            MoveExportUpCommand = new RelayCommand(p => MoveExportUp(p as ExportColumnConfig));
            MoveExportDownCommand = new RelayCommand(p => MoveExportDown(p as ExportColumnConfig));
            ResetExportConfigCommand = new RelayCommand(_ => ResetExportConfig());

            MovePdfUpCommand = new RelayCommand(p => MovePdfUp(p as ExportColumnConfig));
            MovePdfDownCommand = new RelayCommand(p => MovePdfDown(p as ExportColumnConfig));
            ResetPdfConfigCommand = new RelayCommand(_ => ResetPdfConfig());

            ConfigureOptionsCommand = new RelayCommand(p => ConfigureOptions(p as FieldDefinition));
            SaveConfigCommand = new RelayCommand(_ => SaveConfig());
            ApplyConfigCommand = new RelayCommand(_ => SaveConfig());
            ExportConfigCommand = new RelayCommand(_ => ExportConfig());
            ImportConfigCommand = new RelayCommand(_ => ImportConfig());
            ToggleMultiSelectCommand = new RelayCommand(_ => IsMultiSelectMode = !IsMultiSelectMode);
            ToggleExportMultiSelectCommand = new RelayCommand(_ => IsExportMultiSelectMode = !IsExportMultiSelectMode);
            TogglePdfMultiSelectCommand = new RelayCommand(_ => IsPdfMultiSelectMode = !IsPdfMultiSelectMode);
            SelectAllCommand = new RelayCommand(_ => { foreach (var f in Fields) f.IsSelected = true; });
            SelectAllExportCommand = new RelayCommand(_ => { foreach (var c in ExportColumns) c.IsSelected = true; });
            SelectAllPdfCommand = new RelayCommand(_ => { foreach (var c in PdfColumns) c.IsSelected = true; });
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
            DuplicatePautaCommand = new RelayCommand(p => DuplicatePauta(p as PautaSchema));

            AddPresetCommand = new RelayCommand(_ => AddPreset());
            RemovePresetCommand = new RelayCommand(p => RemovePreset(p as ExportPreset));
            RenamePresetCommand = new RelayCommand(p => RenamePreset(p as ExportPreset));

            AvailableTypes = Enum.GetValues(typeof(FieldType)).Cast<FieldType>()
                                .Where(t => t != FieldType.Separator)
                                .ToList();
            EmailMethods = Enum.GetValues(typeof(EmailMethod));

            // Comandos de Reglas PDF
            AddPdfReplacementRuleCommand = new RelayCommand(_ => AddPdfReplacementRule());
            RemovePdfReplacementRuleCommand = new RelayCommand(r => RemovePdfReplacementRule(r as PdfReplacementRule));
            DeleteSelectedPdfRulesCommand = new RelayCommand(_ => DeleteSelectedPdfRules());
            EditPdfRuleFieldsCommand = new RelayCommand(r => EditPdfRuleFields(r as PdfReplacementRule));
            TogglePdfRuleMultiSelectCommand = new RelayCommand(_ => IsPdfRuleMultiSelectMode = !IsPdfRuleMultiSelectMode);
            SelectAllPdfRulesCommand = new RelayCommand(_ =>
            {
                if (EditingPauta != null)
                {
                    foreach (var r in EditingPauta.PdfReplacementRules) r.IsSelected = true;
                }
            });
        }

        public Array EmailMethods { get; }

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
        public ICommand DuplicatePautaCommand { get; }
        public bool WasDatabaseModified { get; private set; } = false;

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
                if (_editingPauta == value) return;

                // 1. Antes de cambiar la pauta, cargamos sus campos de forma "silenciosa" (sin notificar a la UI todavía)
                if (value != null)
                {
                    List<FieldDefinition> config;
                    if (_unsavedConfigs.ContainsKey(value.Id))
                    {
                        config = _unsavedConfigs[value.Id];
                    }
                    else
                    {
                        config = _storageService.LoadConfiguration(value.Id).OrderBy(f => f.Order).ToList();
                    }
                    
                    foreach (var f in config) f.EnsureDefaultOptions();

                    // Actualizar el campo privado directamente
                    _fields = new ObservableCollection<FieldDefinition>(config);
                    foreach (var f in _fields) f.PropertyChanged += OnFieldPropertyChanged;
                    _initialFieldsJson = JsonSerializer.Serialize(_fields);
                    _initialFieldIds = _fields.Select(f => f.Id).OrderBy(id => id).ToList();
                }

                // 2. Cambiar la pauta actual
                _editingPauta = value;

                // 3. Notificar a la UI sobre ambos cambios para que los procese en el mismo ciclo.
                // Es vital notificar que la lista de campos (ItemsSource) ha cambiado ANTES de notificar
                // que la pauta (donde está el SelectedValue) ha cambiado.
                OnPropertyChanged(nameof(Fields));
                OnPropertyChanged(nameof(EditingPauta));

                // 4. Refrescar columnas de exportación/PDF
                if (value != null)
                {
                    LoadExportColumns();
                    LoadPdfColumns();
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

        private ObservableCollection<ExportColumnConfig> _exportColumns = new();
        public ObservableCollection<ExportColumnConfig> ExportColumns
        {
            get => _exportColumns;
            set => SetProperty(ref _exportColumns, value);
        }

        private ObservableCollection<ExportPreset> _exportPresets = new();
        public ObservableCollection<ExportPreset> ExportPresets
        {
            get => _exportPresets;
            set => SetProperty(ref _exportPresets, value);
        }

        private ExportPreset? _selectedExportPreset;
        public ExportPreset? SelectedExportPreset
        {
            get => _selectedExportPreset;
            set
            {
                if (_selectedExportPreset != null && ExportColumns != null)
                {
                    _selectedExportPreset.Columns = ExportColumns.ToList();
                }

                if (SetProperty(ref _selectedExportPreset, value))
                {
                    LoadColumnsFromPreset(value);
                }
            }
        }

        private ObservableCollection<ExportColumnConfig> _pdfColumns = new();
        public ObservableCollection<ExportColumnConfig> PdfColumns
        {
            get => _pdfColumns;
            set => SetProperty(ref _pdfColumns, value);
        }

        public bool IsMultiSelectMode
        {
            get => _isMultiSelectMode;
            set
            {
                if (SetProperty(ref _isMultiSelectMode, value) && !value)
                {
                    foreach (var f in Fields) f.IsSelected = false;
                }
            }
        }

        private bool _isExportMultiSelectMode;
        public bool IsExportMultiSelectMode
        {
            get => _isExportMultiSelectMode;
            set
            {
                if (SetProperty(ref _isExportMultiSelectMode, value) && !value)
                {
                    foreach (var c in ExportColumns) c.IsSelected = false;
                }
            }
        }

        private bool _isPdfMultiSelectMode;
        public bool IsPdfMultiSelectMode
        {
            get => _isPdfMultiSelectMode;
            set
            {
                if (SetProperty(ref _isPdfMultiSelectMode, value) && !value)
                {
                    foreach (var c in PdfColumns) c.IsSelected = false;
                }
            }
        }

        public List<FieldType> AvailableTypes { get; }

        public ICommand AddFieldCommand { get; }
        public ICommand AddSectionCommand { get; }
        public ICommand RemoveFieldCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }

        public ICommand MoveExportUpCommand { get; }
        public ICommand MoveExportDownCommand { get; }
        public ICommand ResetExportConfigCommand { get; }

        public ICommand MovePdfUpCommand { get; }
        public ICommand MovePdfDownCommand { get; }
        public ICommand ResetPdfConfigCommand { get; }

        public ICommand ConfigureOptionsCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand ApplyConfigCommand { get; }
        public ICommand ExportConfigCommand { get; }
        public ICommand ImportConfigCommand { get; }
        public ICommand ToggleMultiSelectCommand { get; }
        public ICommand ToggleExportMultiSelectCommand { get; }
        public ICommand TogglePdfMultiSelectCommand { get; }
        public ICommand AddPresetCommand { get; }
        public ICommand RemovePresetCommand { get; }
        public ICommand RenamePresetCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectAllExportCommand { get; }
        public ICommand SelectAllPdfCommand { get; }
        public ICommand DeleteSelectedCommand { get; }

        public bool HasPendingChanges()
        {
            if (EditingPauta != null)
            {
                string currentFieldsJson = JsonSerializer.Serialize(Fields);
                if (_initialFieldsJson != currentFieldsJson) return true;

                if (SelectedExportPreset != null)
                {
                    SelectedExportPreset.Columns = ExportColumns.OrderBy(c => c.Order).ToList();
                }
                EditingPauta.ExportPresets = ExportPresets.ToList();
                EditingPauta.PdfConfig = PdfColumns.OrderBy(c => c.Order).ToList();
            }

            var savedPautas = _storageService.LoadPautas();
            if (savedPautas.Count != Pautas.Count) return true;
            if (_pautasToDelete.Any()) return true;

            var currentJson = JsonSerializer.Serialize(Pautas);
            var savedJson = JsonSerializer.Serialize(savedPautas);
            
            return currentJson != savedJson;
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
            foreach (var f in Fields) f.PropertyChanged += OnFieldPropertyChanged;
            _initialFieldsJson = JsonSerializer.Serialize(Fields);
            _initialFieldIds = Fields.Select(f => f.Id).OrderBy(id => id).ToList();

            LoadExportColumns();
            LoadPdfColumns();
        }

        private void LoadPdfColumns()
        {
            if (EditingPauta == null) return;

            var existingConfig = EditingPauta.PdfConfig ?? new List<ExportColumnConfig>();
            var newConfig = new ObservableCollection<ExportColumnConfig>();

            // En PDF incluimos todos los campos, INCLUYENDO Secciones (Separadores)
            var validFields = Fields.OrderBy(f => f.Order).ToList();

            var sortedExisting = existingConfig.OrderBy(e => e.Order).ToList();

            foreach (var item in sortedExisting)
            {
                var field = validFields.FirstOrDefault(f => f.Id == item.FieldId);
                if (field != null)
                {
                    if (string.IsNullOrEmpty(item.CustomHeader) || item.CustomHeader == item.OriginalLabel)
                    {
                        item.CustomHeader = field.Label;
                    }
                    item.OriginalLabel = field.Label;
                    item.Type = field.Type;
                    newConfig.Add(item);
                }
            }

            foreach (var field in validFields)
            {
                if (!newConfig.Any(x => x.FieldId == field.Id))
                {
                    newConfig.Add(new ExportColumnConfig
                    {
                        FieldId = field.Id,
                        OriginalLabel = field.Label,
                        Type = field.Type,
                        CustomHeader = field.Label,
                        Order = newConfig.Count,
                        IsVisible = true,
                        IsExportEnabled = true
                    });
                }
            }

            for (int i = 0; i < newConfig.Count; i++) newConfig[i].Order = i;
            PdfColumns = newConfig;
        }

        private void LoadExportColumns()
        {
            if (EditingPauta == null) return;

            // MIGRACIÓN: Si no hay presets pero hay ExportConfig vieja, crear el primer preset
            if ((EditingPauta.ExportPresets == null || !EditingPauta.ExportPresets.Any()) &&
                EditingPauta.ExportConfig != null && EditingPauta.ExportConfig.Any())
            {
                var defaultPreset = new ExportPreset
                {
                    Name = "Predeterminada",
                    Columns = EditingPauta.ExportConfig.Select(c => new ExportColumnConfig
                    {
                        FieldId = c.FieldId,
                        CustomHeader = c.CustomHeader,
                        IsExportEnabled = c.IsExportEnabled,
                        Order = c.Order,
                        OriginalLabel = c.OriginalLabel,
                        Type = c.Type,
                        IsVisible = c.IsVisible
                    }).ToList()
                };
                EditingPauta.ExportPresets = new List<ExportPreset> { defaultPreset };
            }

            // Cargar presets a la colección observable
            ExportPresets = new ObservableCollection<ExportPreset>(EditingPauta.ExportPresets ?? new List<ExportPreset>());

            if (!ExportPresets.Any())
            {
                // Si sigue vacío (pauta nueva), crear uno inicial
                var initial = new ExportPreset { Name = "Configuración Base" };
                ExportPresets.Add(initial);
                EditingPauta.ExportPresets = ExportPresets.ToList();
            }

            SelectedExportPreset = ExportPresets.FirstOrDefault();
        }

        private void ResetExportConfig()
        {
            if (MessageBox.Show("¿Restablecer el orden y nombres de exportación a los valores por defecto?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            var validFields = Fields.Where(f => f.Type != FieldType.Separator).OrderBy(f => f.Order).ToList();
            var newConfig = new ObservableCollection<ExportColumnConfig>();

            for (int i = 0; i < validFields.Count; i++)
            {
                newConfig.Add(new ExportColumnConfig
                {
                    FieldId = validFields[i].Id,
                    OriginalLabel = validFields[i].Label,
                    Type = validFields[i].Type,
                    CustomHeader = validFields[i].Label,
                    Order = i,
                    IsVisible = true,
                    IsExportEnabled = true
                });
            }
            ExportColumns = newConfig;
        }

        private void ResetPdfConfig()
        {
            if (MessageBox.Show("¿Restablecer el orden y nombres del PDF a los valores por defecto?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            var validFields = Fields.OrderBy(f => f.Order).ToList();
            var newConfig = new ObservableCollection<ExportColumnConfig>();

            for (int i = 0; i < validFields.Count; i++)
            {
                newConfig.Add(new ExportColumnConfig
                {
                    FieldId = validFields[i].Id,
                    OriginalLabel = validFields[i].Label,
                    Type = validFields[i].Type,
                    CustomHeader = validFields[i].Label,
                    Order = i,
                    IsVisible = true,
                    IsExportEnabled = true
                });
            }
            PdfColumns = newConfig;
        }

        private void MoveExportUp(ExportColumnConfig? item)
        {
            var selected = ExportColumns.Where(f => f.IsSelected).ToList();
            if (!selected.Any()) { if (item != null) selected.Add(item); else return; }

            var orderedSelected = selected.OrderBy(f => ExportColumns.IndexOf(f)).ToList();
            foreach (var f in orderedSelected)
            {
                int idx = ExportColumns.IndexOf(f);
                if (idx > 0 && !ExportColumns[idx - 1].IsSelected)
                {
                    ExportColumns.Move(idx, idx - 1);
                }
            }
            RecalculateExportOrder();
        }

        private void MoveExportDown(ExportColumnConfig? item)
        {
            var selected = ExportColumns.Where(f => f.IsSelected).ToList();
            if (!selected.Any()) { if (item != null) selected.Add(item); else return; }

            var orderedSelected = selected.OrderByDescending(f => ExportColumns.IndexOf(f)).ToList();
            foreach (var f in orderedSelected)
            {
                int idx = ExportColumns.IndexOf(f);
                if (idx < ExportColumns.Count - 1 && !ExportColumns[idx + 1].IsSelected)
                {
                    ExportColumns.Move(idx, idx + 1);
                }
            }
            RecalculateExportOrder();
        }

        private void RecalculateExportOrder()
        {
            for (int i = 0; i < ExportColumns.Count; i++)
            {
                ExportColumns[i].Order = i;
            }
        }

        private void MovePdfUp(ExportColumnConfig? item)
        {
            var selected = PdfColumns.Where(f => f.IsSelected).ToList();
            if (!selected.Any()) { if (item != null) selected.Add(item); else return; }

            var orderedSelected = selected.OrderBy(f => PdfColumns.IndexOf(f)).ToList();
            foreach (var f in orderedSelected)
            {
                int idx = PdfColumns.IndexOf(f);
                if (idx > 0 && !PdfColumns[idx - 1].IsSelected)
                {
                    PdfColumns.Move(idx, idx - 1);
                }
            }
            RecalculatePdfOrder();
        }

        private void MovePdfDown(ExportColumnConfig? item)
        {
            var selected = PdfColumns.Where(f => f.IsSelected).ToList();
            if (!selected.Any()) { if (item != null) selected.Add(item); else return; }

            var orderedSelected = selected.OrderByDescending(f => PdfColumns.IndexOf(f)).ToList();
            foreach (var f in orderedSelected)
            {
                int idx = PdfColumns.IndexOf(f);
                if (idx < PdfColumns.Count - 1 && !PdfColumns[idx + 1].IsSelected)
                {
                    PdfColumns.Move(idx, idx + 1);
                }
            }
            RecalculatePdfOrder();
        }

        private void RecalculatePdfOrder()
        {
            for (int i = 0; i < PdfColumns.Count; i++)
            {
                PdfColumns[i].Order = i;
            }
        }

        private void AddField()
        {
            var lastField = Fields.OrderBy(f => f.Order).LastOrDefault();
            var newField = new FieldDefinition
            {
                Id = "f_" + Guid.NewGuid().ToString().Substring(0, 4),
                Label = GetNextAvailableLabel("Nuevo campo"),
                Category = lastField?.Category ?? "General",
                Type = FieldType.Text,
                Order = (lastField?.Order ?? 0) + 1,
                MaxLength = 255
            };
            newField.PropertyChanged += OnFieldPropertyChanged;
            Fields.Add(newField);

            // Add to Export list automatically
            ExportColumns.Add(new ExportColumnConfig
            {
                FieldId = newField.Id,
                OriginalLabel = newField.Label,
                Type = newField.Type,
                CustomHeader = newField.Label,
                Order = ExportColumns.Count,
                IsVisible = true,
                IsExportEnabled = true
            });

            // Add to PDF list
            PdfColumns.Add(new ExportColumnConfig
            {
                FieldId = newField.Id,
                OriginalLabel = newField.Label,
                Type = newField.Type,
                CustomHeader = newField.Label,
                Order = PdfColumns.Count,
                IsVisible = true,
                IsExportEnabled = true
            });
        }
        private void AddSection()
        {
            var lastField = Fields.OrderBy(f => f.Order).LastOrDefault();
            var newSection = new FieldDefinition
            {
                Id = "s_" + Guid.NewGuid().ToString().Substring(0, 4),
                Label = GetNextAvailableLabel("Nueva sección"),
                Category = "--- SECCIÓN ---",
                Type = FieldType.Separator,
                Order = (lastField?.Order ?? 0) + 1
            };
            newSection.PropertyChanged += OnFieldPropertyChanged;
            Fields.Add(newSection);

            var newSec = Fields.Last();
            // Add to PDF list automatically
            PdfColumns.Add(new ExportColumnConfig
            {
                FieldId = newSec.Id,
                OriginalLabel = newSec.Label,
                Type = newSec.Type,
                CustomHeader = newSec.Label,
                Order = PdfColumns.Count,
                IsVisible = true,
                IsExportEnabled = true
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
            {
                Fields.Remove(field);
                var exportItem = ExportColumns.FirstOrDefault(x => x.FieldId == field.Id);
                if (exportItem != null) ExportColumns.Remove(exportItem);

                var pdfItem = PdfColumns.FirstOrDefault(x => x.FieldId == field.Id);
                if (pdfItem != null) PdfColumns.Remove(pdfItem);
            }
        }

        private void MoveUp(FieldDefinition? field)
        {
            var selected = Fields.Where(f => f.IsSelected).ToList();
            if (!selected.Any()) { if (field != null) selected.Add(field); else return; }

            var orderedSelected = selected.OrderBy(f => Fields.IndexOf(f)).ToList();
            foreach (var f in orderedSelected)
            {
                int idx = Fields.IndexOf(f);
                if (idx > 0 && !Fields[idx - 1].IsSelected)
                {
                    Fields.Move(idx, idx - 1);
                }
            }
        }

        private void MoveDown(FieldDefinition? field)
        {
            var selected = Fields.Where(f => f.IsSelected).ToList();
            if (!selected.Any()) { if (field != null) selected.Add(field); else return; }

            var orderedSelected = selected.OrderByDescending(f => Fields.IndexOf(f)).ToList();
            foreach (var f in orderedSelected)
            {
                int idx = Fields.IndexOf(f);
                if (idx < Fields.Count - 1 && !Fields[idx + 1].IsSelected)
                {
                    Fields.Move(idx, idx + 1);
                }
            }
        }

        private void ConfigureOptions(FieldDefinition? field)
        {
            if (field == null) return;
            var configurableTypes = new[] { FieldType.Dropdown, FieldType.Boolean, FieldType.Calculation, FieldType.Average, FieldType.Time, FieldType.Text, FieldType.TextArea, FieldType.Numeric };
            if (!configurableTypes.Contains(field.Type)) return;

            var vm = new OptionsEditorViewModel(field, Fields.ToList());
            var win = new OptionsWindow { DataContext = vm };
            win.Owner = System.Windows.Application.Current.MainWindow;
            if (win.ShowDialog() == true)
            {
                field.UseCustomWeights = vm.UseCustomWeights;
                field.MaxLength = vm.ResultMaxLength;
                field.WarnOnDuplicate = vm.WarnOnDuplicate;
                field.TimeFormat = vm.TimeFormat;
                if (field.Type == FieldType.Dropdown)
                {
                    field.Options = vm.ResultOptions;
                    field.AutoSelectRules = vm.ResultAutoSelectRules;
                }
                else if (field.Type == FieldType.Calculation) field.ScoringRules = vm.ResultRules;
                else if (field.Type == FieldType.Average) field.TargetIds = vm.ResultAverageIds;

                field.EnableZeroTrigger = vm.ResultEnableZeroTrigger;
                field.ZeroTriggerFieldId = vm.ResultZeroTriggerFieldId;
                field.ZeroTriggerFieldIds = vm.ResultZeroTriggerFieldIds;
                field.ZeroTriggerValue = vm.ResultZeroTriggerValue;
                field.ShowDecimals = vm.ResultShowDecimals;
                field.Rounding = vm.ResultRounding;

                OnPropertyChanged(nameof(Fields));
            }
        }

        private void PickDate(FieldDefinition? field)
        {
            if (field == null) return;
            var selector = new DateSelectorWindow(field.DefaultValue) { Owner = System.Windows.Application.Current.MainWindow };
            if (selector.ShowDialog() == true)
            {
                field.DefaultValue = selector.SelectedValue == "TODAY" ? DateTime.Now.ToString("dd/MM/yyyy") : selector.SelectedValue;
            }
        }

        private void PickTime(FieldDefinition? field)
        {
            if (field == null) return;
            var selector = new TimeSelectorWindow(field.DefaultValue, field.TimeFormat) { Owner = System.Windows.Application.Current.MainWindow };
            if (selector.ShowDialog() == true)
            {
                field.DefaultValue = selector.SelectedValue == "NOW" ? DateTime.Now.ToString(field.TimeFormat ?? "HH:mm") : selector.SelectedValue;
            }
        }

        private void ExportConfig()
        {
            if (EditingPauta == null) return;

            var settings = _storageService.LoadSettings();
            string exportDir = settings.JsonBackupPath;
            if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

            string fileName = $"Config_{EditingPauta.Name}_{DateTime.Now:yyyyMMdd_HHmm}.json";
            string filePath = Path.Combine(exportDir, fileName);

            try
            {
                var package = new PautaFullExportPackage
                {
                    Metadata = EditingPauta,
                    Fields = Fields.ToList()
                };

                string json = JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);

                if (MessageBox.Show($"Configuración completa exportada con éxito en:\n{filePath}\n\n¿Desea abrir la carpeta ahora?", "Éxito", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    if (Directory.Exists(exportDir)) System.Diagnostics.Process.Start("explorer.exe", exportDir);
                }
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void ImportConfig()
        {
            if (EditingPauta == null) return;

            var ofd = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json" };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(ofd.FileName);

                    // Intentar detectar el formato
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Array)
                        {
                            // Formato antiguo: Solo lista de campos
                            var importedFields = JsonSerializer.Deserialize<ObservableCollection<FieldDefinition>>(json);
                            if (importedFields != null && MessageBox.Show("El archivo solo contiene el diseño de campos. ¿Reemplazar diseño actual?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            {
                                Fields = importedFields;
                                foreach (var f in Fields) f.EnsureDefaultOptions();
                                LoadExportColumns();
                                LoadPdfColumns();
                            }
                        }
                        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Metadata", out _) && root.TryGetProperty("Fields", out _))
                        {
                            // Formato nuevo: Paquete completo
                            var package = JsonSerializer.Deserialize<PautaFullExportPackage>(json);
                            if (package != null && MessageBox.Show("El archivo contiene una configuración COMPLETA (Metadatos, Estructura, PDF, Excel). ¿Reemplazar configuración actual?", "Confirmar Importación Completa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            {
                                // 1. Campos
                                Fields = new ObservableCollection<FieldDefinition>(package.Fields);
                                foreach (var f in Fields) f.EnsureDefaultOptions();

                                // 2. Metadatos (Copiar propiedades al objeto actual para no romper referencias de UI)
                                var m = package.Metadata;
                                var ep = EditingPauta;
                                ep.HelpContent = m.HelpContent;
                                ep.EmailMethod = m.EmailMethod;
                                ep.EmailToTemplate = m.EmailToTemplate;
                                ep.EmailCcTemplate = m.EmailCcTemplate;
                                ep.EmailSubjectTemplate = m.EmailSubjectTemplate;
                                ep.EmailBodyTemplate = m.EmailBodyTemplate;
                                ep.UseAutomatedRecipient = m.UseAutomatedRecipient;
                                ep.EmailNameFieldId = m.EmailNameFieldId;
                                ep.RecipientContacts = m.RecipientContacts ?? new List<RecipientContact>();
                                ep.ExcludeByFieldId = m.ExcludeByFieldId;
                                ep.ExcludeByFieldValue = m.ExcludeByFieldValue;
                                ep.PdfFileNameFieldId1 = m.PdfFileNameFieldId1;
                                ep.PdfFileNameFieldId2 = m.PdfFileNameFieldId2;
                                ep.ExportConfig = m.ExportConfig ?? new List<ExportColumnConfig>();
                                ep.PdfConfig = m.PdfConfig ?? new List<ExportColumnConfig>();
                                ep.ExportPresets = m.ExportPresets ?? new List<ExportPreset>();
                                ep.CounterField1 = m.CounterField1;
                                ep.CounterValue1 = m.CounterValue1;
                                ep.CounterField2 = m.CounterField2;
                                ep.CounterValue2 = m.CounterValue2;
                                ep.CounterField3 = m.CounterField3;
                                ep.CounterValue3 = m.CounterValue3;
                                ep.ColoringField = m.ColoringField;
                                ep.ColoringValue = m.ColoringValue;
                                ep.ColoringColor = m.ColoringColor;
                                ep.EmailReplacementRules = m.EmailReplacementRules ?? new System.Collections.ObjectModel.ObservableCollection<EmailReplacementRule>();
                                ep.PdfReplacementRules = m.PdfReplacementRules ?? new System.Collections.ObjectModel.ObservableCollection<PdfReplacementRule>();

                                LoadExportColumns();
                                LoadPdfColumns();

                                // Guardar en caché de memoria para que persista al cambiar de pauta dentro de la sesión
                                _unsavedConfigs[ep.Id] = Fields.ToList();

                                MessageBox.Show("Configuración importada con éxito en memoria. Recuerde Guardar para confirmar los cambios.");
                            }
                        }
                        else
                        {
                            MessageBox.Show("El formato del archivo JSON no es reconocido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Error al importar: " + ex.Message); }
            }
        }

        private void OnFieldPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is FieldDefinition f && e.PropertyName == nameof(FieldDefinition.Label))
            {
                var exp = ExportColumns.FirstOrDefault(x => x.FieldId == f.Id);
                if (exp != null)
                {
                    if (string.IsNullOrEmpty(exp.CustomHeader) || exp.CustomHeader == exp.OriginalLabel)
                    {
                        exp.CustomHeader = f.Label;
                    }
                    exp.OriginalLabel = f.Label;
                }

                var pdfItem = PdfColumns.FirstOrDefault(x => x.FieldId == f.Id);
                if (pdfItem != null)
                {
                    if (string.IsNullOrEmpty(pdfItem.CustomHeader) || pdfItem.CustomHeader == pdfItem.OriginalLabel)
                    {
                        pdfItem.CustomHeader = f.Label;
                    }
                    pdfItem.OriginalLabel = f.Label;
                }
            }
            if (sender is FieldDefinition f2 && e.PropertyName == nameof(FieldDefinition.Type))
            {
                var exp = ExportColumns.FirstOrDefault(x => x.FieldId == f2.Id);
                if (exp != null) exp.Type = f2.Type;

                var pdfItem = PdfColumns.FirstOrDefault(x => x.FieldId == f2.Id);
                if (pdfItem != null) pdfItem.Type = f2.Type;
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
                foreach (var f in selected)
                {
                    Fields.Remove(f);
                    var exportItem = ExportColumns.FirstOrDefault(x => x.FieldId == f.Id);
                    if (exportItem != null) ExportColumns.Remove(exportItem);
                }
            }
        }

        private void AddPauta()
        {
            var newPauta = new PautaSchema { Name = "Nueva Pauta " + (Pautas.Count + 1) };
            Pautas.Add(newPauta);
            EditingPauta = newPauta;
        }

        private void DuplicatePauta(PautaSchema? source)
        {
            if (source == null) return;

            // Clonar el esquema completo
            var newPauta = new PautaSchema
            {
                Id = Guid.NewGuid().ToString(),
                Name = source.Name + " (Copia)",
                CreatedAt = DateTime.Now,

                // Configuración de Correo
                EmailMethod = source.EmailMethod,
                EmailToTemplate = source.EmailToTemplate,
                EmailCcTemplate = source.EmailCcTemplate,
                EmailSubjectTemplate = source.EmailSubjectTemplate,
                EmailBodyTemplate = source.EmailBodyTemplate,
                UseAutomatedRecipient = source.UseAutomatedRecipient,
                EmailNameFieldId = source.EmailNameFieldId,
                RecipientContacts = source.RecipientContacts?.Select(c => new RecipientContact { Name = c.Name, Email = c.Email }).ToList() ?? new List<RecipientContact>(),

                // Lógica de Exclusión
                ExcludeByFieldId = source.ExcludeByFieldId,
                ExcludeByFieldValue = source.ExcludeByFieldValue,

                // Configuración de PDF
                PdfFileNameFieldId1 = source.PdfFileNameFieldId1,
                PdfFileNameFieldId2 = source.PdfFileNameFieldId2,
                HelpContent = source.HelpContent,

                // Clonar configuraciones de exportación
                ExportConfig = source.ExportConfig?.Select(c => new ExportColumnConfig
                {
                    FieldId = c.FieldId,
                    CustomHeader = c.CustomHeader,
                    IsExportEnabled = c.IsExportEnabled,
                    Order = c.Order,
                    OriginalLabel = c.OriginalLabel,
                    Type = c.Type,
                    IsVisible = c.IsVisible
                }).ToList() ?? new List<ExportColumnConfig>(),

                PdfConfig = source.PdfConfig?.Select(c => new ExportColumnConfig
                {
                    FieldId = c.FieldId,
                    CustomHeader = c.CustomHeader,
                    IsExportEnabled = c.IsExportEnabled,
                    Order = c.Order,
                    OriginalLabel = c.OriginalLabel,
                    Type = c.Type,
                    IsVisible = c.IsVisible
                }).ToList() ?? new List<ExportColumnConfig>(),

                ExportPresets = source.ExportPresets?.Select(p => new ExportPreset
                {
                    Name = p.Name,
                    Columns = p.Columns?.Select(c => new ExportColumnConfig
                    {
                        FieldId = c.FieldId,
                        CustomHeader = c.CustomHeader,
                        IsExportEnabled = c.IsExportEnabled,
                        Order = c.Order,
                        OriginalLabel = c.OriginalLabel,
                        Type = c.Type,
                        IsVisible = c.IsVisible
                    }).ToList() ?? new List<ExportColumnConfig>()
                }).ToList() ?? new List<ExportPreset>(),

                CounterField1 = source.CounterField1,
                CounterValue1 = source.CounterValue1,
                CounterField2 = source.CounterField2,
                CounterValue2 = source.CounterValue2,
                CounterField3 = source.CounterField3,
                CounterValue3 = source.CounterValue3,

                ColoringField = source.ColoringField,
                ColoringValue = source.ColoringValue,
                ColoringColor = source.ColoringColor,

                EmailReplacementRules = new System.Collections.ObjectModel.ObservableCollection<EmailReplacementRule>(
                    source.EmailReplacementRules?.Select(r => new EmailReplacementRule
                    {
                        TargetValue = r.TargetValue,
                        ReplacementValue = r.ReplacementValue,
                        TargetFieldIds = new System.Collections.ObjectModel.ObservableCollection<string>(r.TargetFieldIds ?? new System.Collections.ObjectModel.ObservableCollection<string>())
                    }) ?? Array.Empty<EmailReplacementRule>()
                ),

                PdfReplacementRules = new System.Collections.ObjectModel.ObservableCollection<PdfReplacementRule>(
                    source.PdfReplacementRules?.Select(r => new PdfReplacementRule
                    {
                        TargetValue = r.TargetValue,
                        ReplacementValue = r.ReplacementValue,
                        TextColor = r.TextColor,
                        TargetFieldIds = new System.Collections.ObjectModel.ObservableCollection<string>(r.TargetFieldIds ?? new System.Collections.ObjectModel.ObservableCollection<string>())
                    }) ?? Array.Empty<PdfReplacementRule>()
                )
            };

            // Copiar la configuración de campos (estructura JSON)
            // Revisar si la fuente ya está en memoria (modificada) o si se carga del disco
            var sourceFields = _unsavedConfigs.ContainsKey(source.Id)
                ? _unsavedConfigs[source.Id].Select(f => f.Clone()).ToList()
                : _storageService.LoadConfiguration(source.Id).Select(f => f.Clone()).ToList();

            _unsavedConfigs[newPauta.Id] = sourceFields;

            // Agregar al índice en memoria (NO GUARDAR EN DISCO TODAVÍA)
            Pautas.Add(newPauta);
            WasDatabaseModified = true;

            EditingPauta = newPauta;
            MessageBox.Show($"Pauta '{source.Name}' duplicada con éxito en memoria. Recuerde Guardar para confirmar los cambios.");
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
            var settings = _storageService.LoadSettings();
            string exportDir = settings.JsonBackupPath;
            if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

            string fileName = $"System_Backup_{DateTime.Now:yyyyMMdd_HHmm}.json";
            string filePath = Path.Combine(exportDir, fileName);

            try
            {
                var fullData = new { Pautas = Pautas.ToList(), Configs = Pautas.ToDictionary(p => p.Id, p => _storageService.LoadConfiguration(p.Id)), Records = Pautas.ToDictionary(p => p.Id, p => _storageService.LoadRecords(p.Id)) };
                File.WriteAllText(filePath, JsonSerializer.Serialize(fullData, new JsonSerializerOptions { WriteIndented = true }));

                if (MessageBox.Show($"Respaldo completo exportado con éxito en:\n{filePath}\n\n¿Desea abrir la carpeta ahora?", "Éxito", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    if (Directory.Exists(exportDir)) System.Diagnostics.Process.Start("explorer.exe", exportDir);
                }
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
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
                        // RESPALDO OBLIGATORIO PREVIO A LA ELIMINACIÓN
                        try
                        {
                            var settings = _storageService.LoadSettings();
                            string backupDir = settings.JsonBackupPath;
                            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                            string backupFile = Path.Combine(backupDir, $"System_Backup_Auto_Antes_De_Importar_{DateTime.Now:yyyyMMdd_HHmmss}.json");

                            var currentData = new
                            {
                                Pautas = _storageService.LoadPautas(),
                                Configs = _storageService.LoadPautas().ToDictionary(p => p.Id, p => _storageService.LoadConfiguration(p.Id)),
                                Records = _storageService.LoadPautas().ToDictionary(p => p.Id, p => _storageService.LoadRecords(p.Id))
                            };

                            File.WriteAllText(backupFile, JsonSerializer.Serialize(currentData, new JsonSerializerOptions { WriteIndented = true }));
                            MessageBox.Show($"Se ha creado un respaldo automático de seguridad del sistema actual en:\n{backupFile}", "Respaldo Automático", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            if (MessageBox.Show($"Ocurrió un error al intentar crear el respaldo de seguridad automático:\n{ex.Message}\n\n¿Desea proceder de todas formas bajo su propio riesgo?", "Fallo de Respaldo", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            {
                                return; // Abortar si falló el respaldo y el usuario no quiere continuar
                            }
                        }

                        // CARGAR TODO EN MEMORIA (NO EN DISCO)
                        _unsavedConfigs.Clear();
                        foreach (var kvp in configs) _unsavedConfigs[kvp.Key] = kvp.Value;

                        _unsavedRecords.Clear();
                        if (doc.RootElement.TryGetProperty("Records", out var recsProp))
                        {
                            var records = JsonSerializer.Deserialize<Dictionary<string, List<AuditEntry>>>(recsProp.GetRawText());
                            if (records != null)
                            {
                                foreach (var kvp in records) _unsavedRecords[kvp.Key] = kvp.Value;
                            }
                        }

                        // Actualizar lista de pautas en la UI
                        Pautas = new ObservableCollection<PautaSchema>(pautas);
                        EditingPauta = Pautas.FirstOrDefault();

                        WasDatabaseModified = true;
                        ShouldClearRecords = true; 
                        
                        MessageBox.Show("Base de datos cargada en memoria. Revise los cambios y haga clic en 'Guardar' para aplicarlos permanentemente o en 'Cancelar' para descartarlos.");
                    }
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void SaveConfig()
        {
            IsSaveSuccessful = false;
            ShouldClearRecords = false;
            var settings = _storageService.LoadSettings();

            // 1. Validaciones
            if (Fields.Any(f => string.IsNullOrWhiteSpace(f.Label)))
            {
                MessageBox.Show("Hay campos sin nombre.");
                return;
            }
            var duplicates = Fields.GroupBy(f => f.Label.Trim().ToLower())
                                   .Where(g => g.Count() > 1)
                                   .Select(g => g.First().Label)
                                   .ToList();
            if (duplicates.Any())
            {
                MessageBox.Show($"Hay nombres duplicados:\n{string.Join(", ", duplicates)}", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                var currentFieldIds = Fields.Select(f => f.Id).OrderBy(id => id).ToList();

                // Un cambio es estructural solo si se agregan o eliminan campos/secciones (IDs diferentes)
                bool hasStructuralChanges = _initialFieldIds.Count != currentFieldIds.Count || !_initialFieldIds.SequenceEqual(currentFieldIds);

                if (hasStructuralChanges)
                {
                    var records = _storageService.LoadRecords(EditingPauta.Id);
                    if (records.Any())
                    {
                        var res = MessageBox.Show($"La pauta '{EditingPauta.Name}' tiene {records.Count} registros.\n¿Modificar base de datos y respaldar registros a Excel?", "Cambio Estructural", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                        if (res == MessageBoxResult.Cancel) return;
                        if (res == MessageBoxResult.Yes)
                        {
                            string exportDir = settings.ExcelExportPath;
                            if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

                            string fileName = $"Resp_{EditingPauta.Name}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                            string filePath = Path.Combine(exportDir, fileName);

                            try
                            {
                                // 1. Backup Excel de registros
                                var oldConfig = _storageService.LoadConfiguration(EditingPauta.Id);
                                ExportToExcelInternal(filePath, records, oldConfig, EditingPauta.ExportConfig);

                                // 2. Backup JSON de configuración (la que corresponde a esos registros)
                                string jsonDir = settings.JsonBackupPath;
                                if (!Directory.Exists(jsonDir)) Directory.CreateDirectory(jsonDir);
                                string jsonPath = Path.Combine(jsonDir, $"Config_Resp_{EditingPauta.Name}_{DateTime.Now:yyyyMMdd_HHmm}.json");
                                File.WriteAllText(jsonPath, JsonSerializer.Serialize(oldConfig, new JsonSerializerOptions { WriteIndented = true }));

                                MessageBox.Show($"Respaldos realizados con éxito:\n- Excel: {filePath}\n- JSON: {jsonPath}");

                                if (MessageBox.Show("¿Desea abrir la carpeta de exportación?", "Respaldos realizados", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                                {
                                    if (Directory.Exists(exportDir)) System.Diagnostics.Process.Start("explorer.exe", exportDir);
                                }

                                // Limpiar registros en disco
                                _storageService.SaveRecords(EditingPauta.Id, new List<AuditEntry>());

                                // Si es la pauta activa en Main, avisar para limpiar UI
                                if (EditingPauta.Id == _activePautaIdInMain) ShouldClearRecords = true;
                            }
                            catch (Exception ex) { MessageBox.Show("Error al realizar respaldos: " + ex.Message); return; }
                        }
                        else return;
                    }
                }

                // SAVE EXPORT CONFIG
                if (SelectedExportPreset != null)
                {
                    SelectedExportPreset.Columns = ExportColumns.OrderBy(c => c.Order).ToList();
                }
                EditingPauta.ExportPresets = ExportPresets.ToList();
                EditingPauta.PdfConfig = PdfColumns.OrderBy(c => c.Order).ToList();

                _storageService.BackupConfiguration(EditingPauta.Id);
                _storageService.SaveConfiguration(EditingPauta.Id, list);
                _initialFieldsJson = currentFieldsJson;
                _initialFieldIds = currentFieldIds;
            }

            // 4. Procesar eliminaciones
            foreach (var p in _pautasToDelete)
            {
                var records = _storageService.LoadRecords(p.Id);
                if (records.Any() && MessageBox.Show($"La pauta '{p.Name}' tiene registros. ¿Respaldar a Excel antes de borrar?", "Eliminación", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string exportDir = settings.ExcelExportPath;
                    if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);
                    string filePath = Path.Combine(exportDir, $"Final_{p.Name}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");

                    try
                    {
                        ExportToExcelInternal(filePath, records, _storageService.LoadConfiguration(p.Id), p.ExportConfig);
                        MessageBox.Show($"Respaldo final guardado en:\n{filePath}");
                    }
                    catch (Exception ex) { MessageBox.Show("Error al respaldar pauta borrada: " + ex.Message); }
                }

                // Respaldo JSON si el archivo existe
                string appDataStruct = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PautaDinamica", "users", SessionService.CurrentUser?.Username ?? "default");
                string configPath = Path.Combine(appDataStruct, $"pauta_{p.Id}_config.json");
                if (File.Exists(configPath))
                {
                    if (MessageBox.Show($"¿Respaldar JSON de '{p.Name}' antes de borrar?", "Borrar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        string jsonDir = settings.JsonBackupPath;
                        if (!Directory.Exists(jsonDir)) Directory.CreateDirectory(jsonDir);
                        string jsonPath = Path.Combine(jsonDir, $"Backup_{p.Name}_{DateTime.Now:yyyyMMdd_HHmm}.json");

                        try
                        {
                            File.WriteAllText(jsonPath, JsonSerializer.Serialize(_storageService.LoadConfiguration(p.Id), new JsonSerializerOptions { WriteIndented = true }));
                            MessageBox.Show($"Configuración respaldada en:\n{jsonPath}");
                        }
                        catch (Exception ex) { MessageBox.Show("Error respaldo JSON: " + ex.Message); }
                    }
                }

                _storageService.DeletePautaFiles(p.Id);
            }
            _pautasToDelete.Clear();

            // 4. Guardar configuraciones y registros pendientes en memoria
            foreach (var kvp in _unsavedConfigs)
            {
                _storageService.SaveConfiguration(kvp.Key, kvp.Value);
            }
            _unsavedConfigs.Clear();

            foreach (var kvp in _unsavedRecords)
            {
                _storageService.SaveRecords(kvp.Key, kvp.Value);
            }
            _unsavedRecords.Clear();

            // 5. Guardar índice
            _storageService.SavePautas(Pautas.ToList());
            IsSaveSuccessful = true;
            WasDatabaseModified = true;
            MessageBox.Show("Cambios guardados con éxito.");
        }

        private void ExportToExcelInternal(string filePath, List<AuditEntry> records, List<FieldDefinition> rawFields, List<ExportColumnConfig> exportConfig)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Auditoría");
                var exportCols = new List<(string Id, string Header, FieldType Type)>();

                if (exportConfig != null && exportConfig.Any())
                {
                    // Usar orden personalizado
                    var orderedConfig = exportConfig.OrderBy(c => c.Order).ToList();
                    var configuredIds = new HashSet<string>(exportConfig.Select(x => x.FieldId));

                    foreach (var cfg in orderedConfig)
                    {
                        var f = rawFields.FirstOrDefault(rf => rf.Id == cfg.FieldId);
                        if (f != null && cfg.IsVisible && cfg.IsExportEnabled)
                        {
                            exportCols.Add((f.Id, cfg.CustomHeader, f.Type));
                        }
                    }

                    // Agregar campos nuevos que no estén en la config
                    foreach (var f in rawFields)
                    {
                        if (!configuredIds.Contains(f.Id) && f.Type != FieldType.Separator)
                            exportCols.Add((f.Id, f.Label, f.Type));
                    }
                }
                else
                {
                    // Orden natural por defecto
                    foreach (var f in rawFields.Where(f => f.Type != FieldType.Separator))
                        exportCols.Add((f.Id, f.Label, f.Type));
                }

                // --- CABECERAS ---
                int headerStartCol = 1;
                var settings = _storageService.LoadSettings();
                if (settings.EnableInternalTimer)
                {
                    worksheet.Cell(1, 1).Value = "Duración";
                    worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerStartCol = 2;
                }

                for (int i = 0; i < exportCols.Count; i++)
                {
                    worksheet.Cell(1, i + headerStartCol).Value = exportCols[i].Header;
                }

                // --- DATOS ---
                int row = 2;
                foreach (var entry in records)
                {
                    int col = 1;
                    if (settings.EnableInternalTimer)
                    {
                        // Excel almacena el tiempo como una fracción del día (1 día = 1440 min)
                        worksheet.Cell(row, 1).Value = entry.InternalDurationMinutes / 1440.0;
                        worksheet.Cell(row, 1).Style.NumberFormat.Format = "[mm]:ss";
                        col = 2;
                    }

                    foreach (var colDef in exportCols)
                    {
                        if (entry.Values.TryGetValue(colDef.Id, out var val))
                        {
                            string strVal = val?.ToString() ?? "";
                            var cell = worksheet.Cell(row, col);

                            if (colDef.Type == FieldType.Boolean)
                            {
                                bool? valResult = null;
                                if (val is bool b) valResult = b;
                                else if (strVal == "1") valResult = true;
                                else if (strVal == "0") valResult = false;
                                else if (bool.TryParse(strVal, out bool boolVal)) valResult = boolVal;

                                if (valResult.HasValue)
                                {
                                    cell.Value = valResult.Value ? 1 : 0;
                                    cell.Style.NumberFormat.Format = "0";
                                }
                                else cell.Value = strVal;
                            }
                            else if (colDef.Type == FieldType.Numeric || colDef.Type == FieldType.Calculation || colDef.Type == FieldType.Average)
                            {
                                if (strVal.Contains("%"))
                                {
                                    string cleanVal = strVal.Replace("%", "").Trim();
                                    if (double.TryParse(cleanVal, out double pctVal))
                                    {
                                        cell.Value = pctVal / 100.0;
                                        var fieldDef = rawFields.FirstOrDefault(f => f.Id == colDef.Id);
                                        string excelFormat = (fieldDef?.ShowDecimals ?? true) ? "0.0%" : "0%";
                                        cell.Style.NumberFormat.Format = excelFormat;
                                    }
                                    else cell.Value = strVal;
                                }
                                else if (double.TryParse(strVal, out double numVal))
                                {
                                    cell.Value = numVal;
                                    var fieldDef = rawFields.FirstOrDefault(f => f.Id == colDef.Id);
                                    string excelFormat = (fieldDef?.ShowDecimals ?? true) ? "0.00" : "0";
                                    cell.Style.NumberFormat.Format = excelFormat;
                                }
                                else cell.Value = strVal;
                            }
                            else if (colDef.Type == FieldType.Date && DateTime.TryParse(strVal, out DateTime dt))
                            {
                                cell.Value = dt;
                            }
                            else if (colDef.Type == FieldType.Time && DateTime.TryParse(strVal, out DateTime tm))
                            {
                                cell.Value = tm.TimeOfDay;
                                cell.Style.NumberFormat.Format = "HH:mm:ss";
                            }
                            else cell.Value = strVal;

                            if (colDef.Type == FieldType.TextArea || strVal.Contains("\n"))
                                cell.Style.Alignment.SetWrapText(true);
                        }
                        col++;
                    }
                    row++;
                }
                worksheet.Columns().AdjustToContents();
                foreach (var c in worksheet.Columns()) { if (c.Width > 50) c.Width = 50; }
                workbook.SaveAs(filePath);
            }
        }
        private void LoadColumnsFromPreset(ExportPreset? preset)
        {
            if (preset == null || EditingPauta == null) return;

            var existingConfig = preset.Columns ?? new List<ExportColumnConfig>();
            var newConfig = new ObservableCollection<ExportColumnConfig>();

            // Solo mostrar campos reales, no secciones
            var validFields = Fields.Where(f => f.Type != FieldType.Separator).OrderBy(f => f.Order).ToList();

            var sortedExisting = existingConfig.OrderBy(e => e.Order).ToList();

            foreach (var item in sortedExisting)
            {
                var field = validFields.FirstOrDefault(f => f.Id == item.FieldId);
                if (field != null)
                {
                    if (string.IsNullOrEmpty(item.CustomHeader) || item.CustomHeader == item.OriginalLabel)
                    {
                        item.CustomHeader = field.Label;
                    }
                    item.OriginalLabel = field.Label;
                    item.Type = field.Type; // Update Type
                    newConfig.Add(item);
                }
            }

            // Agregar campos nuevos que no estaban en la config
            foreach (var field in validFields)
            {
                if (!newConfig.Any(x => x.FieldId == field.Id))
                {
                    newConfig.Add(new ExportColumnConfig
                    {
                        FieldId = field.Id,
                        OriginalLabel = field.Label,
                        Type = field.Type, // Set Type
                        CustomHeader = field.Label,
                        Order = newConfig.Count,
                        IsVisible = true,
                        IsExportEnabled = true
                    });
                }
            }

            // Re-indexar para asegurar orden limpio
            for (int i = 0; i < newConfig.Count; i++) newConfig[i].Order = i;

            ExportColumns = newConfig;
        }

        private void AddPreset()
        {
            if (EditingPauta == null) return;
            var newPreset = new ExportPreset { Name = "Nueva Configuración " + (ExportPresets.Count + 1) };

            // Copiar la configuración actual como base
            if (SelectedExportPreset != null)
            {
                newPreset.Columns = ExportColumns.Select(c => new ExportColumnConfig
                {
                    FieldId = c.FieldId,
                    CustomHeader = c.CustomHeader,
                    IsExportEnabled = c.IsExportEnabled,
                    Order = c.Order,
                    OriginalLabel = c.OriginalLabel,
                    Type = c.Type,
                    IsVisible = c.IsVisible
                }).ToList();
            }

            ExportPresets.Add(newPreset);
            SelectedExportPreset = newPreset;
        }

        private void RemovePreset(ExportPreset? preset)
        {
            if (preset == null) return;
            if (ExportPresets.Count <= 1)
            {
                MessageBox.Show("Debe existir al menos una configuración de exportación.");
                return;
            }

            if (MessageBox.Show($"¿Eliminar la configuración '{preset.Name}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                ExportPresets.Remove(preset);
                SelectedExportPreset = ExportPresets.FirstOrDefault();
            }
        }

        private void RenamePreset(ExportPreset? preset)
        {
            if (preset == null) return;

            // Por simplicidad, usamos un InputBox improvisado o solo permitimos editar el nombre si tuviéramos un TextBox bindeado.
            // En la UI de ConfigWindow usaremos un TextBox bindeado al nombre del preset seleccionado.
        }
        // --- Comandos de Reglas PDF ---
        public ICommand AddPdfReplacementRuleCommand { get; }
        public ICommand RemovePdfReplacementRuleCommand { get; }
        public ICommand TogglePdfRuleMultiSelectCommand { get; }
        public ICommand DeleteSelectedPdfRulesCommand { get; }
        public ICommand SelectAllPdfRulesCommand { get; }
        public ICommand EditPdfRuleFieldsCommand { get; }

        private bool _isPdfRuleMultiSelectMode;
        public bool IsPdfRuleMultiSelectMode { get => _isPdfRuleMultiSelectMode; set => SetProperty(ref _isPdfRuleMultiSelectMode, value); }


        private void AddPdfReplacementRule()
        {
            if (EditingPauta == null) return;
            var rule = new PdfReplacementRule
            {
                TargetValue = "1",
                ReplacementValue = "Cumple",
                TextColor = "#28a745" // Verde por defecto
            };

            // Pre-seleccionar primer campo si hay
            var firstField = Fields.FirstOrDefault(f => f.Type != FieldType.Separator);
            if (firstField != null) rule.TargetFieldIds.Add(firstField.Id);

            EditingPauta.PdfReplacementRules.Add(rule);
        }

        private void RemovePdfReplacementRule(PdfReplacementRule? rule)
        {
            if (EditingPauta != null && rule != null)
            {
                if (MessageBox.Show("¿Eliminar esta regla de reemplazo PDF?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    EditingPauta.PdfReplacementRules.Remove(rule);
                }
            }
        }

        private void DeleteSelectedPdfRules()
        {
            if (EditingPauta == null) return;
            var toRemove = EditingPauta.PdfReplacementRules.Where(r => r.IsSelected).ToList();
            if (toRemove.Count == 0) return;

            if (MessageBox.Show($"¿Eliminar las {toRemove.Count} reglas PDF seleccionadas?", "Confirmar Eliminación Múltiple", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var r in toRemove) EditingPauta.PdfReplacementRules.Remove(r);
            }
        }

        private void EditPdfRuleFields(PdfReplacementRule? rule)
        {
            if (rule == null) return;

            var selectableFields = Fields.Where(f => f.Type != FieldType.Separator).Select(f => new SelectableFieldViewModel
            {
                Id = f.Id,
                Label = f.Label,
                IsSelected = rule.TargetFieldIds.Contains(f.Id)
            }).ToList();

            var win = new MultiFieldSelectorWindow(selectableFields);
            win.Owner = System.Windows.Application.Current.Windows.OfType<ConfigWindow>().FirstOrDefault();

            if (win.ShowDialog() == true)
            {
                rule.TargetFieldIds.Clear();
                foreach (var sf in selectableFields.Where(x => x.IsSelected))
                {
                    rule.TargetFieldIds.Add(sf.Id);
                }
            }
        }
    }
}
