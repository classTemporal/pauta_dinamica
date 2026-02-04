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
        private string _activePautaIdInMain; // Track which pauta is currently open in Main window

        private ObservableCollection<PautaSchema> _pautas = new();
        private PautaSchema? _editingPauta;
        private bool _isPautaMultiSelectMode;
        private bool _isMultiSelectMode;
        private readonly List<PautaSchema> _pautasToDelete = new();
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
            DuplicatePautaCommand = new RelayCommand(p => DuplicatePauta(p as PautaSchema));

            AvailableTypes = Enum.GetValues(typeof(FieldType)).Cast<FieldType>()
                                .Where(t => t != FieldType.Separator)
                                .ToList();
            EmailMethods = Enum.GetValues(typeof(EmailMethod));
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

        private ObservableCollection<ExportColumnConfig> _exportColumns = new();
        public ObservableCollection<ExportColumnConfig> ExportColumns
        {
            get => _exportColumns;
            set => SetProperty(ref _exportColumns, value);
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
            set => SetProperty(ref _isMultiSelectMode, value);
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
                    item.OriginalLabel = field.Label;
                    item.Type = field.Type;
                    if (string.IsNullOrEmpty(item.CustomHeader)) item.CustomHeader = field.Label;
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

            var existingConfig = EditingPauta.ExportConfig ?? new List<ExportColumnConfig>();
            var newConfig = new ObservableCollection<ExportColumnConfig>();

            // Solo mostrar campos reales, no secciones
            var validFields = Fields.Where(f => f.Type != FieldType.Separator).OrderBy(f => f.Order).ToList();

            // Estrategia: 
            // 1. Tomar los que ya existen en la config y ordenarlos según su orden guardado
            // 2. Agregar los nuevos al final

            var sortedExisting = existingConfig.OrderBy(e => e.Order).ToList();

            foreach (var item in sortedExisting)
            {
                var field = validFields.FirstOrDefault(f => f.Id == item.FieldId);
                if (field != null)
                {
                    item.OriginalLabel = field.Label;
                    item.Type = field.Type; // Update Type
                    // Asegurar consistencia
                    if (string.IsNullOrEmpty(item.CustomHeader)) item.CustomHeader = field.Label;
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
            if (item == null) return;
            int index = ExportColumns.IndexOf(item);
            if (index > 0)
            {
                ExportColumns.Move(index, index - 1);
                RecalculateExportOrder();
            }
        }

        private void MoveExportDown(ExportColumnConfig? item)
        {
            if (item == null) return;
            int index = ExportColumns.IndexOf(item);
            if (index < ExportColumns.Count - 1)
            {
                ExportColumns.Move(index, index + 1);
                RecalculateExportOrder();
            }
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
            if (item == null) return;
            int index = PdfColumns.IndexOf(item);
            if (index > 0)
            {
                PdfColumns.Move(index, index - 1);
                RecalculatePdfOrder();
            }
        }

        private void MovePdfDown(ExportColumnConfig? item)
        {
            if (item == null) return;
            int index = PdfColumns.IndexOf(item);
            if (index < PdfColumns.Count - 1)
            {
                PdfColumns.Move(index, index + 1);
                RecalculatePdfOrder();
            }
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
            Fields.Add(new FieldDefinition
            {
                Id = "s_" + Guid.NewGuid().ToString().Substring(0, 4),
                Label = GetNextAvailableLabel("Nueva sección"),
                Category = "--- SECCIÓN ---",
                Type = FieldType.Separator,
                Order = (lastField?.Order ?? 0) + 1
            });

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
            var win = new OptionsWindow { DataContext = vm };
            win.Owner = System.Windows.Application.Current.MainWindow;
            if (win.ShowDialog() == true)
            {
                field.UseCustomWeights = vm.UseCustomWeights;
                field.MaxLength = vm.ResultMaxLength;
                field.WarnOnDuplicate = vm.WarnOnDuplicate;
                field.TimeFormat = vm.TimeFormat;
                if (field.Type == FieldType.Dropdown) field.Options = vm.ResultOptions;
                else if (field.Type == FieldType.Calculation) field.ScoringRules = vm.ResultRules;
                else if (field.Type == FieldType.Average) field.TargetIds = vm.ResultAverageIds;

                field.EnableZeroTrigger = vm.ResultEnableZeroTrigger;
                field.ZeroTriggerFieldId = vm.ResultZeroTriggerFieldId;
                field.ZeroTriggerValue = vm.ResultZeroTriggerValue;
                field.ShowDecimals = vm.ResultShowDecimals;

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
            var settings = _storageService.LoadSettings();
            string exportDir = settings.JsonBackupPath;
            if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

            string fileName = $"Config_{EditingPauta?.Name}_{DateTime.Now:yyyyMMdd_HHmm}.json";
            string filePath = Path.Combine(exportDir, fileName);

            try
            {
                string json = JsonSerializer.Serialize(Fields, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);

                if (MessageBox.Show($"Configuración exportada con éxito en:\n{filePath}\n\n¿Desea abrir la carpeta ahora?", "Éxito", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    if (Directory.Exists(exportDir)) System.Diagnostics.Process.Start("explorer.exe", exportDir);
                }
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
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
                        LoadExportColumns(); // Refresh export columns to match new imported fields
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

            // Clonar el esquema básico
            var newPauta = new PautaSchema
            {
                Id = Guid.NewGuid().ToString(),
                Name = source.Name + " (Copia)",
                CreatedAt = DateTime.Now,
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
                }).ToList() ?? new List<ExportColumnConfig>()
            };

            // Copiar la configuración de campos (estructura JSON)
            var sourceFields = _storageService.LoadConfiguration(source.Id);
            // IMPORTANTE: Los campos dentro de la configuración deben tener los mismos IDs para que el ExportConfig/PdfConfig funcionen
            _storageService.SaveConfiguration(newPauta.Id, sourceFields);

            // Agregar al índice
            Pautas.Add(newPauta);
            _storageService.SavePautas(Pautas.ToList());

            EditingPauta = newPauta;
            MessageBox.Show($"Pauta '{source.Name}' duplicada con éxito como '{newPauta.Name}'");
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
            var settings = _storageService.LoadSettings();

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
                            string exportDir = settings.ExcelExportPath;
                            if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

                            string fileName = $"Resp_{EditingPauta.Name}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                            string filePath = Path.Combine(exportDir, fileName);

                            try
                            {
                                // 1. Backup Excel de registros
                                var oldConfig = _storageService.LoadConfiguration(EditingPauta.Id);
                                ExportToExcelInternal(filePath, records, oldConfig);

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
                EditingPauta.ExportConfig = ExportColumns.OrderBy(c => c.Order).ToList();
                EditingPauta.PdfConfig = PdfColumns.OrderBy(c => c.Order).ToList();

                _storageService.BackupConfiguration(EditingPauta.Id);
                _storageService.SaveConfiguration(EditingPauta.Id, list);
                _initialFieldsJson = currentFieldsJson;
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
                        ExportToExcelInternal(filePath, records, _storageService.LoadConfiguration(p.Id));
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
