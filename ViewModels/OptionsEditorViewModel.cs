using PautaDinamicaApp;
using PautaDinamicaApp.Models;
using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Win32;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace PautaDinamicaApp.ViewModels
{
    public class SelectableFieldVM : ViewModelBase
    {
        private bool _isSelected;
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    OnChanged?.Invoke();
                }
            }
        }
        public Action? OnChanged { get; set; }
    }

    public class SelectableOptionVM : ViewModelBase
    {
        private string _text = "";
        private bool _isSelected;
        private object? _tag;
        public string Text { get => _text; set => SetProperty(ref _text, value); }
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
        public object? Tag { get => _tag; set => SetProperty(ref _tag, value); }
        public SelectableOptionVM(string text) { Text = text; }
    }

    public class ValueScoreVM : ViewModelBase
    {
        private string _value = "";
        private string _scoreText = "1";
        private double _score = 1.0;
        private bool _isDisabled;

        public string Value { get => _value; set => SetProperty(ref _value, value); }
        public bool IsDisabled { get => _isDisabled; set => SetProperty(ref _isDisabled, value); }
        public override bool IsValid => true;

        public double Score
        {
            get => _score;
            set
            {
                if (SetProperty(ref _score, value))
                    _scoreText = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        // Propiedad String para el TextBox para evitar errores de conversion con cadena vacia
        public string ScoreText
        {
            get => _scoreText;
            set
            {
                if (SetProperty(ref _scoreText, value))
                {
                    if (double.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                    {
                        _score = result;
                    }
                    else
                    {
                        _score = 0; // Si esta vacio o es invalido, tratamos como 0 para calculo
                    }
                    OnPropertyChanged(nameof(Score));
                }
            }
        }
    }

    public class RuleEditorVM : ViewModelBase
    {
        private bool _isActive;
        private string _percentageText = "0";
        private double _percentage;
        private string _naValue = "N/A";
        private Action? _onChanged;

        public RuleEditorVM(Action? onChanged = null) { _onChanged = onChanged; }

        public string FieldId { get; set; } = "";
        public string FieldLabel { get; set; } = "";
        public override bool IsValid => true;

        public bool IsActive
        {
            get => _isActive;
            set { if (SetProperty(ref _isActive, value)) _onChanged?.Invoke(); }
        }

        public double Percentage
        {
            get => _percentage;
            set
            {
                if (SetProperty(ref _percentage, value))
                {
                    _percentageText = value.ToString(CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(PercentageText));
                }
            }
        }

        // Propiedad String para el TextBox para evitar errores de conversion con cadena vacia
        public string PercentageText
        {
            get => _percentageText;
            set
            {
                if (SetProperty(ref _percentageText, value))
                {
                    if (double.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                    {
                        _percentage = result;
                    }
                    else
                    {
                        _percentage = 0;
                    }
                    OnPropertyChanged(nameof(Percentage));
                    _onChanged?.Invoke();
                }
            }
        }

        public string NaValue
        {
            get => _naValue;
            set { if (SetProperty(ref _naValue, value)) UpdateDisabledStates(); }
        }

        public ObservableCollection<ValueScoreVM> Mappings { get; } = new();

        public void UpdateDisabledStates()
        {
            foreach (var m in Mappings)
                m.IsDisabled = m.Value.Equals(NaValue, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class AutoSelectRuleVM : ViewModelBase
    {
        private string _sourceFieldId = "";
        private string _operator = "=";
        private string _value = "";
        private string _targetValue = "";

        public string SourceFieldId { get => _sourceFieldId; set => SetProperty(ref _sourceFieldId, value); }
        public string Operator { get => _operator; set => SetProperty(ref _operator, value); }
        public string Value { get => _value; set => SetProperty(ref _value, value); }
        public string TargetValue { get => _targetValue; set => SetProperty(ref _targetValue, value); }
        public override bool IsValid => true;
    }

    public class OptionsEditorViewModel : ViewModelBase
    {
        private readonly StorageService _storageService = new StorageService();
        private ObservableCollection<SelectableOptionVM> _options = new();
        private string _newOptionText = string.Empty;
        private bool _isMultiSelectMode;
        private bool _useCustomWeights;
        private string _validationError = "";
        private string _timeFormat = "HH:mm";
        private bool _internalUpdate;
        private bool _enableZeroTrigger;
        private string _zeroTriggerValue = string.Empty;
        private bool _showDecimals;
        private CalculationRounding _rounding;
        private bool _allowMultipleAttachments;
        private bool _attachToEmail;
        private int _maxLength;
        private bool _allowAnyFile;
        private string? _pautaId;
        private ObservableCollection<ExtensionItem> _availableExtensions = new();
        private ObservableCollection<SelectableFieldVM> _triggerFieldChoices = new();

        public FieldDefinition OriginalField { get; }
        public ObservableCollection<RuleEditorVM> CalculationRules { get; } = new();
        public ObservableCollection<SelectableOptionVM> AverageTargets { get; } = new();
        public ObservableCollection<AutoSelectRuleVM> AutoSelectRules { get; } = new();
        public List<string> Operators { get; } = new() { "=", ">", "<", ">=", "<=" };

        public OptionsEditorViewModel(FieldDefinition field, List<FieldDefinition> allFields, string? pautaId = null)
        {
            OriginalField = field;
            _pautaId = pautaId;
            _useCustomWeights = field.UseCustomWeights;
            _timeFormat = field.TimeFormat;
            _maxLength = field.MaxLength;
            WarnOnDuplicate = field.WarnOnDuplicate;
            ShowDecimals = field.ShowDecimals;
            Rounding = field.Rounding;
            AllowMultipleAttachments = field.AllowMultipleAttachments;
            AttachToEmail = field.AttachToEmail;
            AllowAnyFile = field.AllowAnyFile;

            InitializeExtensions(field.AllowedExtensions);

            // Initialize AutoSelectRules from field
            if (field.AutoSelectRules != null)
            {
                foreach (var r in field.AutoSelectRules)
                {
                    AutoSelectRules.Add(new AutoSelectRuleVM
                    {
                        SourceFieldId = r.SourceFieldId,
                        Operator = r.Operator,
                        Value = r.Value,
                        TargetValue = r.TargetValue
                    });
                }
            }

            var wrapped = (field.Options ?? new List<string>()).Select(s => new SelectableOptionVM(s));
            Options = new ObservableCollection<SelectableOptionVM>(wrapped);

            _internalUpdate = true;
            if (field.Type == FieldType.Calculation)
            {
                var candidates = allFields.Where(f => f.Id != field.Id && (f.Type == FieldType.Dropdown || f.Type == FieldType.Boolean)).ToList();

                foreach (var f in candidates)
                {
                    var existing = field.ScoringRules.FirstOrDefault(r => r.FieldId == f.Id);
                    var rule = new RuleEditorVM(OnRuleChanged)
                    {
                        FieldId = f.Id,
                        FieldLabel = f.Label,
                        IsActive = existing != null,
                        Percentage = existing?.Weight ?? 0,
                        NaValue = existing?.NaValue ?? "N/A"
                    };

                    if (f.Type == FieldType.Dropdown)
                    {
                        foreach (var opt in f.Options)
                        {
                            var m = existing?.Mappings.FirstOrDefault(x => x.Value == opt);
                            rule.Mappings.Add(new ValueScoreVM
                            {
                                Value = opt,
                                Score = m?.Score ?? (opt.Equals("Cumple", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0)
                            });
                        }
                    }
                    else if (f.Type == FieldType.Boolean)
                    {
                        var mTrue = existing?.Mappings.FirstOrDefault(x => x.Value == "1" || x.Value == "True");
                        var mFalse = existing?.Mappings.FirstOrDefault(x => x.Value == "0" || x.Value == "False");
                        rule.Mappings.Add(new ValueScoreVM { Value = "1 (Marcado)", Score = mTrue?.Score ?? 1.0 });
                        rule.Mappings.Add(new ValueScoreVM { Value = "0 (Desmarcado)", Score = mFalse?.Score ?? 0.0 });
                    }
                    rule.UpdateDisabledStates();
                    CalculationRules.Add(rule);
                }

                _internalUpdate = false; // Permitimos que OnRuleChanged funcione para las llamadas siguientes

                if (field.ScoringRules.Count == 0 || needsInitialRedistribution(field))
                {
                    RedistributeEqually();
                }

                ValidatePercentages();
            }
            else
            {
                _internalUpdate = false;
            }

            if (field.Type == FieldType.Average)
            {
                var candidates = allFields.Where(f => f.Id != field.Id && (f.Type == FieldType.Calculation || f.Type == FieldType.Average));
                foreach (var f in candidates)
                {
                    AverageTargets.Add(new SelectableOptionVM(f.Label) { Tag = f.Id, IsSelected = field.TargetIds.Contains(f.Id) });
                }
            }

            // Zero Trigger Initialization
            AvailableFields = allFields.Where(f => f.Id != field.Id && f.Type != FieldType.Separator).ToList();
            _enableZeroTrigger = field.EnableZeroTrigger;
            _zeroTriggerValue = field.ZeroTriggerValue;

            // Compatibilidad: Si ZeroTriggerFieldId tiene valor pero ZeroTriggerFieldIds está vacío, migrar
            var initialIds = field.ZeroTriggerFieldIds ?? new List<string>();
            if (initialIds.Count == 0 && !string.IsNullOrEmpty(field.ZeroTriggerFieldId))
            {
                initialIds = new List<string> { field.ZeroTriggerFieldId };
            }

            foreach (var f in AvailableFields)
            {
                TriggerFieldChoices.Add(new SelectableFieldVM
                {
                    Id = f.Id,
                    Label = f.Label,
                    IsSelected = initialIds.Contains(f.Id),
                    OnChanged = () =>
                    {
                        OnPropertyChanged(nameof(SelectedTriggerCount));
                        OnPropertyChanged(nameof(TriggerSelectionsSummary));
                    }
                });
            }

            OnPropertyChanged(nameof(SelectedTriggerCount));
            OnPropertyChanged(nameof(TriggerSelectionsSummary));

            Options.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ResultOptions));



            AddOptionCommand = new RelayCommand(_ => AddOption(), _ => !string.IsNullOrWhiteSpace(NewOptionText));
            RemoveOptionCommand = new RelayCommand(p => RemoveOption(p as SelectableOptionVM));
            ToggleMultiSelectCommand = new RelayCommand(_ => ToggleMultiSelect());
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected());
            ExportOptionsCommand = new RelayCommand(_ => ExportToExcel(Options, "Opciones"));
            ImportOptionsCommand = new RelayCommand(_ => ImportFromExcel());
            ResetCalculationCommand = new RelayCommand(_ => ResetCalculation());

            AddAutoSelectRuleCommand = new RelayCommand(_ => AutoSelectRules.Add(new AutoSelectRuleVM()));
            RemoveAutoSelectRuleCommand = new RelayCommand(r => { if (r is AutoSelectRuleVM vm) AutoSelectRules.Remove(vm); });

            MoveUpCommand = new RelayCommand(p => MoveUp(p as SelectableOptionVM));
            MoveDownCommand = new RelayCommand(p => MoveDown(p as SelectableOptionVM));

            OpenFolderCommand = new RelayCommand(_ =>
            {
                if (string.IsNullOrEmpty(_pautaId)) return;
                string path = _storageService.GetPautaAttachmentsDir(_pautaId);
                if (System.IO.Directory.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", path);
                }
                else
                {
                    MessageBoxHelper.ShowNonCritical("La carpeta de archivos aún no ha sido creada o no contiene archivos.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }

        private bool needsInitialRedistribution(FieldDefinition f)
        {
            // Si todos los pesos son <= 1.0, probablemente vienen de la lógica antigua o están en 0
            return f.ScoringRules.Any() && f.ScoringRules.All(r => r.Weight <= 1.0);
        }

        private void OnRuleChanged()
        {
            if (_internalUpdate) return;

            if (!UseCustomWeights)
            {
                RedistributeEqually();
            }

            ValidatePercentages();
        }

        private void RedistributeEqually()
        {
            _internalUpdate = true;
            try
            {
                var active = CalculationRules.Where(r => r.IsActive).ToList();
                if (active.Any())
                {
                    double share = Math.Round(100.0 / active.Count, 2);
                    foreach (var r in active) r.Percentage = share;

                    double total = active.Sum(r => r.Percentage);
                    if (Math.Abs(total - 100) > 0.001) active.Last().Percentage += (100 - total);
                }
                else
                {
                    foreach (var r in CalculationRules) r.Percentage = 0;
                }
            }
            finally
            {
                _internalUpdate = false;
            }
        }

        private void ResetCalculation()
        {
            _internalUpdate = true;
            try
            {
                foreach (var r in CalculationRules)
                {
                    r.IsActive = false;
                    r.Percentage = 0;
                }
                // No llamamos a RedistributeEqually aqui porque queremos que todo este en 0
                // Pero si hay campos por defecto que el usuario suele querer, 
                // el comportamiento de "deselect all" es literal lo solicitado.
            }
            finally
            {
                _internalUpdate = false;
            }
            ValidatePercentages();
        }

        public string ValidationError { get => _validationError; set => SetProperty(ref _validationError, value); }
        public bool HasError => !string.IsNullOrEmpty(ValidationError);
        public override bool IsValid => !HasError;

        private void ValidatePercentages()
        {
            if (!UseCustomWeights) { ValidationError = ""; OnPropertyChanged(nameof(HasError)); return; }

            var active = CalculationRules.Where(r => r.IsActive).ToList();
            if (!active.Any()) { ValidationError = "⚠️ Debe activar al menos un campo para el cálculo"; OnPropertyChanged(nameof(HasError)); return; }

            double total = active.Sum(r => r.Percentage);
            if (total > 100.001)
                ValidationError = $"⚠️ Error: La suma ({total:F1}%) excede el 100%";
            else if (total < 99.99)
                ValidationError = $"⚠️ Error: La suma ({total:F1}%) es menor al 100%";
            else
                ValidationError = "";

            OnPropertyChanged(nameof(HasError));
        }

        public List<FieldDefinition> AvailableFields { get; }

        public bool EnableZeroTrigger
        {
            get => _enableZeroTrigger;
            set => SetProperty(ref _enableZeroTrigger, value);
        }

        public ObservableCollection<SelectableFieldVM> TriggerFieldChoices
        {
            get => _triggerFieldChoices;
            set => SetProperty(ref _triggerFieldChoices, value);
        }

        public string ZeroTriggerValue
        {
            get => _zeroTriggerValue;
            set => SetProperty(ref _zeroTriggerValue, value);
        }

        public ObservableCollection<SelectableOptionVM> Options { get => _options; set => SetProperty(ref _options, value); }
        public string NewOptionText { get => _newOptionText; set => SetProperty(ref _newOptionText, value); }
        public bool IsMultiSelectMode { get => _isMultiSelectMode; set => SetProperty(ref _isMultiSelectMode, value); }
        public bool UseCustomWeights
        {
            get => _useCustomWeights;
            set
            {
                if (SetProperty(ref _useCustomWeights, value))
                {
                    if (value) RedistributeEqually();
                    ValidatePercentages();
                }
            }
        }

        public string TimeFormat { get => _timeFormat; set => SetProperty(ref _timeFormat, value); }
        public int MaxLength { get => _maxLength; set => SetProperty(ref _maxLength, value); }

        private bool _warnOnDuplicate;
        public bool WarnOnDuplicate { get => _warnOnDuplicate; set => SetProperty(ref _warnOnDuplicate, value); }

        public List<string> ResultOptions => Options.Select(o => o.Text).ToList();
        public List<ScoringRule> ResultRules => CalculationRules.Where(r => r.IsActive).Select(r => new ScoringRule
        {
            FieldId = r.FieldId,
            Weight = r.Percentage,
            NaValue = r.NaValue,
            Mappings = r.Mappings.Select(m => new ValueScoreMapping { Value = m.Value.Contains("(") ? m.Value.Split(' ')[0] : m.Value, Score = m.Score }).ToList()
        }).ToList();
        public int ResultMaxLength => MaxLength;
        public List<string> ResultAverageIds => AverageTargets.Where(t => t.IsSelected).Select(t => t.Tag?.ToString() ?? "").ToList();

        public bool ResultEnableZeroTrigger => EnableZeroTrigger;
        public string ResultZeroTriggerFieldId => TriggerFieldChoices.FirstOrDefault(t => t.IsSelected)?.Id ?? "";
        public List<string> ResultZeroTriggerFieldIds => TriggerFieldChoices.Where(t => t.IsSelected).Select(t => t.Id).ToList();
        public string ResultZeroTriggerValue => ZeroTriggerValue;
        public bool ResultShowDecimals => ShowDecimals;
        public CalculationRounding ResultRounding => Rounding;

        public bool ShowDecimals
        {
            get => _showDecimals;
            set => SetProperty(ref _showDecimals, value);
        }

        public CalculationRounding Rounding
        {
            get => _rounding;
            set => SetProperty(ref _rounding, value);
        }



        public bool AllowMultipleAttachments
        {
            get => _allowMultipleAttachments;
            set => SetProperty(ref _allowMultipleAttachments, value);
        }

        public bool AttachToEmail
        {
            get => _attachToEmail;
            set => SetProperty(ref _attachToEmail, value);
        }

        public bool AllowAnyFile
        {
            get => _allowAnyFile;
            set 
            { 
                if (SetProperty(ref _allowAnyFile, value) && value)
                {
                    // If allowing any, deselect all specific ones
                    foreach (var ext in AvailableExtensions) ext.IsSelected = false;
                }
            }
        }

        public ObservableCollection<ExtensionItem> AvailableExtensions
        {
            get => _availableExtensions;
            set => SetProperty(ref _availableExtensions, value);
        }

        public List<string> ResultAllowedExtensions => 
            AvailableExtensions.Where(e => e.IsSelected).Select(e => e.Extension).ToList();

        public Array AvailableRoundingModes => Enum.GetValues(typeof(CalculationRounding));

        public int SelectedTriggerCount => TriggerFieldChoices.Count(t => t.IsSelected);
        public string TriggerSelectionsSummary
        {
            get
            {
                var selected = TriggerFieldChoices.Where(t => t.IsSelected).ToList();
                if (selected.Count == 0) return "Seleccionar Campos...";
                if (selected.Count == 1) return selected[0].Label;
                return $"{selected.Count} campos seleccionados";
            }
        }

        public ICommand AddOptionCommand { get; }
        public ICommand RemoveOptionCommand { get; }
        public ICommand ToggleMultiSelectCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ExportOptionsCommand { get; }
        public ICommand ImportOptionsCommand { get; }
        public ICommand ResetCalculationCommand { get; }

        public ICommand AddAutoSelectRuleCommand { get; }
        public ICommand RemoveAutoSelectRuleCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand OpenFolderCommand { get; }

        public List<AutoSelectRule> ResultAutoSelectRules => AutoSelectRules.Select(r => new AutoSelectRule
        {
            SourceFieldId = r.SourceFieldId,
            Operator = r.Operator,
            Value = r.Value,
            TargetValue = r.TargetValue
        }).ToList();

        private string LoadPautaName()
        {
            if (string.IsNullOrEmpty(_pautaId)) return "SinPauta";
            try
            {
                var pauta = _storageService.LoadPautas().FirstOrDefault(p => p.Id == _pautaId);
                return string.IsNullOrWhiteSpace(pauta?.Name) ? "SinPauta" : pauta.Name;
            }
            catch { return "SinPauta"; }
        }

        private void ExportToExcel(IEnumerable<SelectableOptionVM> list, string baseName)
        {
            var items = list.ToList();
            if (!items.Any()) return;

            var settings = _storageService.LoadSettings();
            string exportFolder = settings.ExcelExportPath;
            string pautaName = LoadPautaName();
            string fileName = $"{baseName}_{pautaName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string finalPath = "";

            if (System.IO.Directory.Exists(exportFolder))
            {
                finalPath = System.IO.Path.Combine(exportFolder, fileName);
            }
            else
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = fileName,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                if (sfd.ShowDialog() == true) finalPath = sfd.FileName;
                else return;
            }

            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Opciones");
                    worksheet.Cell(1, 1).Value = "Opción";
                    worksheet.Cell(1, 1).Style.Font.Bold = true;
                    for (int i = 0; i < items.Count; i++) worksheet.Cell(i + 2, 1).Value = items[i].Text;
                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(finalPath);
                    MessageBoxHelper.ShowNonCritical($"Opciones exportadas correctamente en:\n{finalPath}", "Exportación Exitosa");
                }
            }
            catch (Exception ex) { MessageBoxHelper.Show("Error al exportar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void ImportFromExcel()
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

                        var newOptions = new List<string>();
                        // Empezamos desde la fila 2 asumiendo encabezado (como en la exportacion)
                        // Si no hay encabezado o el usuario quiere todo, podriamos revisar fila 1
                        var rows = worksheet.RowsUsed();
                        foreach (var row in rows)
                        {
                            var val = row.Cell(1).Value.ToString().Trim();
                            if (string.IsNullOrWhiteSpace(val) || val == "Opción") continue;

                            if (!Options.Any(o => o.Text.Equals(val, StringComparison.OrdinalIgnoreCase)) &&
                                !newOptions.Any(o => o.Equals(val, StringComparison.OrdinalIgnoreCase)))
                            {
                                newOptions.Add(val);
                            }
                        }

                        if (newOptions.Any())
                        {
                            var result = MessageBoxHelper.ShowNonCritical($"Se encontraron {newOptions.Count} nuevas opciones. ¿Desea agregarlas?",
                                "Importar Opciones", MessageBoxButton.YesNo, MessageBoxImage.Question);

                            if (result == MessageBoxResult.Yes)
                            {
                                foreach (var opt in newOptions)
                                {
                                    Options.Add(new SelectableOptionVM(opt));
                                }
                            }
                        }
                        else
                        {
                            MessageBoxHelper.ShowNonCritical("No se encontraron nuevas opciones válidas para importar.", "Importar", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBoxHelper.Show("Error al importar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddOption()
        {
            if (string.IsNullOrWhiteSpace(NewOptionText)) return;
            string cleaned = NewOptionText.Trim();
            if (!Options.Any(o => o.Text.Equals(cleaned, StringComparison.OrdinalIgnoreCase))) Options.Add(new SelectableOptionVM(cleaned));
            NewOptionText = string.Empty;
        }
        private void RemoveOption(SelectableOptionVM? o)
        {
            if (o != null)
            {
                var result = MessageBoxHelper.Show($"¿Estás seguro de que deseas eliminar la opción '{o.Text}'?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning, true);
                if (result == MessageBoxResult.Yes)
                {
                    Options.Remove(o);
                }
            }
        }
        private void ToggleMultiSelect() { IsMultiSelectMode = !IsMultiSelectMode; if (!IsMultiSelectMode) foreach (var o in Options) o.IsSelected = false; }
        private void SelectAll() { bool all = Options.All(o => o.IsSelected); foreach (var o in Options) o.IsSelected = !all; }
        private void DeleteSelected()
        {
            var sel = Options.Where(o => o.IsSelected).ToList();
            if (sel.Any())
            {
                var result = MessageBoxHelper.Show($"¿Estás seguro de que deseas eliminar las {sel.Count} opciones seleccionadas?", "Confirmar Eliminación Múltiple", MessageBoxButton.YesNo, MessageBoxImage.Warning, true);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var s in sel) Options.Remove(s);
                }
            }
        }

        private void MoveUp(SelectableOptionVM? item)
        {
            var selected = Options.Where(o => o.IsSelected).ToList();
            if (!selected.Any())
            {
                if (item != null) selected.Add(item);
                else return;
            }

            var orderedSelected = selected.OrderBy(o => Options.IndexOf(o)).ToList();
            foreach (var o in orderedSelected)
            {
                int idx = Options.IndexOf(o);
                if (idx > 0 && !Options[idx - 1].IsSelected)
                {
                    Options.Move(idx, idx - 1);
                }
            }
        }

        private void MoveDown(SelectableOptionVM? item)
        {
            var selected = Options.Where(o => o.IsSelected).ToList();
            if (!selected.Any())
            {
                if (item != null) selected.Add(item);
                else return;
            }

            var orderedSelected = selected.OrderByDescending(o => Options.IndexOf(o)).ToList();
            foreach (var o in orderedSelected)
            {
                int idx = Options.IndexOf(o);
                if (idx < Options.Count - 1 && !Options[idx + 1].IsSelected)
                {
                    Options.Move(idx, idx + 1);
                }
            }
        }

        private void InitializeExtensions(List<string> selected)
        {
            var categories = new Dictionary<string, string[]>
            {
                { "Imágenes", new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" } },
                { "Documentos", new[] { ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".csv" } },
                { "Audio", new[] { ".mp3", ".wav", ".m4a", ".flac", ".ogg" } },
                { "Video", new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv" } },
                { "Otros", new[] { ".zip", ".rar", ".7z" } }
            };

            foreach (var cat in categories)
            {
                foreach (var ext in cat.Value)
                {
                    var item = new ExtensionItem 
                    { 
                        Extension = ext, 
                        Category = cat.Key,
                        IsSelected = selected?.Contains(ext) == true
                    };

                    item.PropertyChanged += (s, e) => {
                        if (e.PropertyName == nameof(ExtensionItem.IsSelected) && item.IsSelected)
                        {
                            // Si se selecciona uno específico, desactivar "Cualquier archivo"
                            AllowAnyFile = false;
                        }
                    };

                    AvailableExtensions.Add(item);
                }
            }
        }
    }

    public class ExtensionItem : ViewModelBase
    {
        private bool _isSelected;
        public string Extension { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set => SetProperty(ref _isSelected, value); 
        }
    }
}
