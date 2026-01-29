using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Win32;
using PautaDinamicaApp.Models;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace PautaDinamicaApp.ViewModels
{
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
                    _percentageText = value.ToString(CultureInfo.InvariantCulture);
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

    public class OptionsEditorViewModel : ViewModelBase
    {
        private ObservableCollection<SelectableOptionVM> _options = new();
        private string _newOptionText = string.Empty;
        private bool _isMultiSelectMode;
        private bool _useCustomWeights;
        private string _validationError = "";
        private string _timeFormat = "HH:mm";
        private bool _internalUpdate;

        public FieldDefinition OriginalField { get; }
        public ObservableCollection<RuleEditorVM> CalculationRules { get; } = new();
        public ObservableCollection<SelectableOptionVM> AverageTargets { get; } = new();

        public OptionsEditorViewModel(FieldDefinition field, List<FieldDefinition> allFields)
        {
            OriginalField = field;
            _useCustomWeights = field.UseCustomWeights;
            _timeFormat = field.TimeFormat;
            _maxLength = field.MaxLength;
            WarnOnDuplicate = field.WarnOnDuplicate;

            var wrapped = (field.Options ?? new List<string>()).Select(s => new SelectableOptionVM(s));
            Options = new ObservableCollection<SelectableOptionVM>(wrapped);

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
                        var mTrue = existing?.Mappings.FirstOrDefault(x => x.Value == "True");
                        var mFalse = existing?.Mappings.FirstOrDefault(x => x.Value == "False");
                        rule.Mappings.Add(new ValueScoreVM { Value = "True (Marcado)", Score = mTrue?.Score ?? 1.0 });
                        rule.Mappings.Add(new ValueScoreVM { Value = "False (Desmarcado)", Score = mFalse?.Score ?? 0.0 });
                    }
                    rule.UpdateDisabledStates();
                    CalculationRules.Add(rule);
                }

                if (field.ScoringRules.Count == 0 || needsInitialRedistribution(field))
                {
                    RedistributeEqually();
                }

                ValidatePercentages();
            }

            if (field.Type == FieldType.Average)
            {
                var candidates = allFields.Where(f => f.Id != field.Id && f.Type == FieldType.Calculation);
                foreach (var f in candidates)
                {
                    AverageTargets.Add(new SelectableOptionVM(f.Label) { Tag = f.Id, IsSelected = field.TargetIds.Contains(f.Id) });
                }
            }

            AddOptionCommand = new RelayCommand(_ => AddOption(), _ => !string.IsNullOrWhiteSpace(NewOptionText));
            RemoveOptionCommand = new RelayCommand(p => RemoveOption(p as SelectableOptionVM));
            ToggleMultiSelectCommand = new RelayCommand(_ => ToggleMultiSelect());
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected());
            ExportOptionsCommand = new RelayCommand(_ => ExportToExcel(Options, "Opciones"));
            ImportOptionsCommand = new RelayCommand(_ => ImportFromExcel());
        }

        private bool needsInitialRedistribution(FieldDefinition f)
        {
            // Si todos los pesos son <= 1.0, probablemente vienen de la lógica antigua o están en 0
            return f.ScoringRules.Any() && f.ScoringRules.All(r => r.Weight <= 1.0);
        }

        private void OnRuleChanged()
        {
            if (_internalUpdate) return;
            ValidatePercentages();
        }

        private void RedistributeEqually()
        {
            _internalUpdate = true;
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
            _internalUpdate = false;
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
        private int _maxLength;
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

        public ICommand AddOptionCommand { get; }
        public ICommand RemoveOptionCommand { get; }
        public ICommand ToggleMultiSelectCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ExportOptionsCommand { get; }
        public ICommand ImportOptionsCommand { get; }

        private void ExportToExcel(IEnumerable<SelectableOptionVM> list, string baseName)
        {
            var items = list.ToList();
            if (!items.Any()) return;
            var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmm}" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Opciones");
                        worksheet.Cell(1, 1).Value = "Opción";
                        for (int i = 0; i < items.Count; i++) worksheet.Cell(i + 2, 1).Value = items[i].Text;
                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(sfd.FileName);
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void ImportFromExcel()
        {
            var ofd = new OpenFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx" };
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
                            var result = MessageBox.Show($"Se encontraron {newOptions.Count} nuevas opciones. ¿Desea agregarlas?",
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
                            MessageBox.Show("No se encontraron nuevas opciones válidas para importar.", "Importar", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al importar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var result = MessageBox.Show($"¿Estás seguro de que deseas eliminar la opción '{o.Text}'?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
                var result = MessageBox.Show($"¿Estás seguro de que deseas eliminar las {sel.Count} opciones seleccionadas?", "Confirmar Eliminación Múltiple", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var s in sel) Options.Remove(s);
                }
            }
        }
    }
}
