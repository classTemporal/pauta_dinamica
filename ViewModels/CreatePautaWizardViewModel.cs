using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace PautaDinamicaApp.ViewModels
{
    /// <summary>
    /// Step definition for the pauta creation wizard.
    /// Each step has a title, description, and an index.
    /// </summary>
    public class WizardStep : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isCompleted;

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Index { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set { _isCompleted = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// ViewModel for the "Crear nueva pauta" wizard (Card 39).
    /// Guides the user through 3 steps:
    /// 1. Field list configuration (add/edit/remove/define field types)
    /// 2. Dashboard display order (reorder without affecting main list order)
    /// 3. Email configuration (method, templates, recipients, exclusion)
    /// </summary>
    public class CreatePautaWizardViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;
        private int _currentStep;
        private string _pautaName = string.Empty;
        private ObservableCollection<FieldDefinition> _fields;
        private ObservableCollection<FieldDisplayModel> _dashboardFields;
        private EmailMethod _selectedEmailMethod = EmailMethod.Mailto;
        private string _emailToTemplate = string.Empty;
        private string _emailCcTemplate = string.Empty;
        private string _emailSubjectTemplate = string.Empty;
        private string _emailBodyTemplate = string.Empty;
        private bool _useAutomatedRecipient;
        private string _emailNameFieldId = string.Empty;
        private ObservableCollection<RecipientContact> _recipientContacts;

        // Exclusion logic
        private string _excludeByFieldId = string.Empty;
        private string _excludeByFieldValue = string.Empty;

        public ObservableCollection<WizardStep> Steps { get; }

        public CreatePautaWizardViewModel()
        {
            _storageService = new StorageService();
            _fields = new ObservableCollection<FieldDefinition>();
            _dashboardFields = new ObservableCollection<FieldDisplayModel>();
            _recipientContacts = new ObservableCollection<RecipientContact>();

            // Initialize wizard name
            try
            {
                var pautas = _storageService.LoadPautas();
                PautaName = "Nueva Pauta " + (pautas.Count + 1);
            }
            catch { PautaName = "Nueva Pauta"; }

            // Define the 3 steps
            Steps = new ObservableCollection<WizardStep>
            {
                new WizardStep { Index = 0, Title = "Campos de la Pauta", Description = "Configure los campos que tendrá la pauta.", IsSelected = true },
                new WizardStep { Index = 1, Title = "Orden en Dashboard", Description = "Organice el orden de visualización de los campos." },
                new WizardStep { Index = 2, Title = "Configuración de Correos", Description = "Defina cómo se enviarán los correos de las auditorías." }
            };

            AvailableTypes = Enum.GetValues(typeof(FieldType)).Cast<FieldType>()
                .Where(t => t != FieldType.Separator)
                .OrderBy(t => t switch
                {
                    FieldType.Text => "Texto corto",
                    FieldType.Numeric => "Número",
                    FieldType.Date => "Fecha",
                    FieldType.Time => "Hora",
                    FieldType.Dropdown => "Lista de elementos",
                    FieldType.Calculation => "Porcentaje",
                    FieldType.Boolean => "Binario",
                    FieldType.Average => "Promedio",
                    FieldType.TextArea => "Texto largo",
                    FieldType.FileAttachment => "Archivo Adjunto",
                    _ => t.ToString()
                })
                .ToList();

            EmailMethods = Enum.GetValues(typeof(EmailMethod));

            // Commands
            AddFieldCommand = new RelayCommand(_ => AddField());
            AddSectionCommand = new RelayCommand(_ => AddSection());
            RemoveFieldCommand = new RelayCommand(p => RemoveField(p as FieldDefinition));
            MoveUpCommand = new RelayCommand(p => MoveUp(p as FieldDefinition));
            MoveDownCommand = new RelayCommand(p => MoveDown(p as FieldDefinition));
            ConfigureOptionsCommand = new RelayCommand(p => ConfigureOptions(p as FieldDefinition));
            MoveDashboardUpCommand = new RelayCommand(p => MoveDashboardUp(p as FieldDisplayModel));
            MoveDashboardDownCommand = new RelayCommand(p => MoveDashboardDown(p as FieldDisplayModel));
            NextStepCommand = new RelayCommand(_ => NextStep(), _ => CanMoveToNextStep());
            PreviousStepCommand = new RelayCommand(_ => PreviousStep());
            FinishCommand = new RelayCommand(_ => Finish(), _ => CanFinish());
            CancelCommand = new RelayCommand(_ => Cancel());
            PickDateCommand = new RelayCommand(p => PickDate(p as FieldDefinition));
            PickTimeCommand = new RelayCommand(p => PickTime(p as FieldDefinition));
            AddContactCommand = new RelayCommand(_ => AddContact());
            RemoveContactCommand = new RelayCommand(p => RemoveContact(p as RecipientContact));

            // Add a default field so the wizard starts with something
            AddField();
        }

        public string PautaName
        {
            get => _pautaName;
            set
            {
                if (SetProperty(ref _pautaName, value))
                {
                    Steps[0].IsCompleted = !string.IsNullOrWhiteSpace(value);
                }
            }
        }

        public List<FieldType> AvailableTypes { get; }
        public Array EmailMethods { get; }

        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (SetProperty(ref _currentStep, value))
                {
                    UpdateStepStates();
                }
            }
        }

        public ObservableCollection<FieldDefinition> Fields
        {
            get => _fields;
            set => SetProperty(ref _fields, value);
        }

        public ObservableCollection<FieldDisplayModel> DashboardFields
        {
            get => _dashboardFields;
            set => SetProperty(ref _dashboardFields, value);
        }

        public EmailMethod SelectedEmailMethod
        {
            get => _selectedEmailMethod;
            set => SetProperty(ref _selectedEmailMethod, value);
        }

        public string EmailToTemplate
        {
            get => _emailToTemplate;
            set => SetProperty(ref _emailToTemplate, value);
        }

        public string EmailCcTemplate
        {
            get => _emailCcTemplate;
            set => SetProperty(ref _emailCcTemplate, value);
        }

        public string EmailSubjectTemplate
        {
            get => _emailSubjectTemplate;
            set => SetProperty(ref _emailSubjectTemplate, value);
        }

        public string EmailBodyTemplate
        {
            get => _emailBodyTemplate;
            set => SetProperty(ref _emailBodyTemplate, value);
        }

        public bool UseAutomatedRecipient
        {
            get => _useAutomatedRecipient;
            set
            {
                if (SetProperty(ref _useAutomatedRecipient, value))
                {
                    OnPropertyChanged(nameof(ShowContactDirectory));
                }
            }
        }

        public string EmailNameFieldId
        {
            get => _emailNameFieldId;
            set => SetProperty(ref _emailNameFieldId, value);
        }

        public ObservableCollection<RecipientContact> RecipientContacts
        {
            get => _recipientContacts;
            set => SetProperty(ref _recipientContacts, value);
        }

        public bool ShowContactDirectory => UseAutomatedRecipient;

        // Exclusion
        public string ExcludeByFieldId
        {
            get => _excludeByFieldId;
            set => SetProperty(ref _excludeByFieldId, value);
        }

        public string ExcludeByFieldValue
        {
            get => _excludeByFieldValue;
            set => SetProperty(ref _excludeByFieldValue, value);
        }

        // --- Commands ---
        public ICommand AddFieldCommand { get; }
        public ICommand AddSectionCommand { get; }
        public ICommand RemoveFieldCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand ConfigureOptionsCommand { get; }
        public ICommand MoveDashboardUpCommand { get; }
        public ICommand MoveDashboardDownCommand { get; }
        public ICommand NextStepCommand { get; }
        public ICommand PreviousStepCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand PickDateCommand { get; }
        public ICommand PickTimeCommand { get; }
        public ICommand AddContactCommand { get; }
        public ICommand RemoveContactCommand { get; }

        // Result property — the created PautaSchema (available only after Finish)
        public PautaSchema? CreatedPauta { get; private set; }

        private void UpdateStepStates()
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                Steps[i].IsSelected = i == CurrentStep;
                Steps[i].IsCompleted = i < CurrentStep || (i == 0 && !string.IsNullOrWhiteSpace(PautaName));
            }

            // Rebuild dashboard fields when entering/exiting step 1
            if (CurrentStep == 1)
            {
                RefreshDashboardFields();
            }

            OnPropertyChanged(nameof(CanMoveToNextStep));
            OnPropertyChanged(nameof(CanFinish));
            OnPropertyChanged(nameof(IsAtLastStep));
        }

        public bool IsAtLastStep => CurrentStep == Steps.Count - 1;

        private bool CanMoveToNextStep()
        {
            if (CurrentStep == 0)
            {
                // Step 0 requires at least a name and at least one field
                return !string.IsNullOrWhiteSpace(PautaName) && Fields.Any(f => f.Type != FieldType.Separator);
            }
            return true;
        }

        private bool CanFinish()
        {
            return CurrentStep == Steps.Count - 1 && !string.IsNullOrWhiteSpace(PautaName) && Fields.Any(f => f.Type != FieldType.Separator);
        }

        private void NextStep()
        {
            if (CurrentStep < Steps.Count - 1)
            {
                CurrentStep++;
            }
        }

        private void PreviousStep()
        {
            if (CurrentStep > 0)
            {
                CurrentStep--;
            }
        }

        private void RefreshDashboardFields()
        {
            var displayModels = Fields.Where(f => f.Type != FieldType.Separator)
                .Select(f => new FieldDisplayModel
                {
                    FieldId = f.Id,
                    Label = f.Label,
                    Type = f.Type,
                    IsVisible = true,
                    Order = DashboardFields.FirstOrDefault(d => d.FieldId == f.Id)?.Order ?? DashboardFields.Count
                }).ToList();

            // Preserve existing order for fields that already exist
            if (displayModels.Any() && DashboardFields.Any())
            {
                int order = 0;
                foreach (var dm in displayModels)
                {
                    dm.Order = order++;
                }
            }

            DashboardFields = new ObservableCollection<FieldDisplayModel>(displayModels);
        }

        private void Finish()
        {
            if (!CanFinish()) return;

            // Build the PautaSchema with all configured data
            var newPauta = new PautaSchema
            {
                Name = PautaName,
                CreatedAt = DateTime.Now,
                EmailMethod = SelectedEmailMethod,
                EmailToTemplate = EmailToTemplate,
                EmailCcTemplate = EmailCcTemplate,
                EmailSubjectTemplate = EmailSubjectTemplate,
                EmailBodyTemplate = EmailBodyTemplate,
                UseAutomatedRecipient = UseAutomatedRecipient,
                EmailNameFieldId = EmailNameFieldId,
                RecipientContacts = RecipientContacts.ToList(),
                ExcludeByFieldId = ExcludeByFieldId,
                ExcludeByFieldValue = ExcludeByFieldValue,
                DashboardFieldOrder = DashboardFields.Where(d => d.IsVisible).Select(d => d.FieldId).ToList()
            };

            // Ensure fields are ordered
            var fieldList = Fields.ToList();
            string currentBox = "General";
            for (int i = 0; i < fieldList.Count; i++)
            {
                fieldList[i].Order = i;
                if (fieldList[i].Type == FieldType.Separator)
                {
                    currentBox = fieldList[i].Label;
                    fieldList[i].Category = "--- SECCIÓN ---";
                }
                else
                {
                    fieldList[i].Category = currentBox;
                    fieldList[i].EnsureDefaultOptions();
                }
            }

            // Save the configuration
            _storageService.SaveConfiguration(newPauta.Id, fieldList);

            // Save the pauta in the list
            var pautas = _storageService.LoadPautas();
            pautas.Insert(0, newPauta);
            _storageService.SavePautas(pautas);
            _storageService.SetLastPautaId(newPauta.Id);

            CreatedPauta = newPauta;

            // Signal completion
            OnWizardCompleted?.Invoke(this, new());
        }

        public event EventHandler<EventArgs>? OnWizardCompleted;

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

            // Rebuild dashboard fields
            RefreshDashboardFields();
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
            if (field == null) return;
            var result = MessageBox.Show($"¿Eliminar campo [{field.Label}]?", "Confirmar", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                Fields.Remove(field);
                RefreshDashboardFields();
            }
        }

        private void MoveUp(FieldDefinition? field)
        {
            if (field == null) return;
            int idx = Fields.IndexOf(field);
            if (idx > 0)
            {
                Fields.Move(idx, idx - 1);
            }
        }

        private void MoveDown(FieldDefinition? field)
        {
            if (field == null) return;
            int idx = Fields.IndexOf(field);
            if (idx < Fields.Count - 1)
            {
                Fields.Move(idx, idx + 1);
            }
        }

        private void MoveDashboardUp(FieldDisplayModel? item)
        {
            if (item == null) return;
            int idx = DashboardFields.IndexOf(item);
            if (idx > 0)
            {
                DashboardFields.Move(idx, idx - 1);
                // Recalculate order
                for (int i = 0; i < DashboardFields.Count; i++)
                    DashboardFields[i].Order = i;
                item.Order = idx - 1;
            }
        }

        private void MoveDashboardDown(FieldDisplayModel? item)
        {
            if (item == null) return;
            int idx = DashboardFields.IndexOf(item);
            if (idx < DashboardFields.Count - 1)
            {
                DashboardFields.Move(idx, idx + 1);
                for (int i = 0; i < DashboardFields.Count; i++)
                    DashboardFields[i].Order = i;
                item.Order = idx + 1;
            }
        }

        private void ConfigureOptions(FieldDefinition? field)
        {
            if (field == null) return;
            var configurableTypes = new[] { FieldType.Dropdown, FieldType.Boolean, FieldType.Calculation, FieldType.Average, FieldType.Time, FieldType.Text, FieldType.TextArea, FieldType.Numeric, FieldType.FileAttachment };
            if (!configurableTypes.Contains(field.Type)) return;

            var vm = new OptionsEditorViewModel(field, Fields.ToList(), null);
            var win = new Views.OptionsWindow { DataContext = vm };
            win.Owner = System.Windows.Application.Current.MainWindow;

            if (win.ShowDialog() == true)
            {
                field.UseCustomWeights = vm.UseCustomWeights;
                field.MaxLength = vm.ResultMaxLength;
                field.WarnOnDuplicate = vm.WarnOnDuplicate;
                field.TimeFormat = vm.TimeFormat;
                field.AllowedExtensions = vm.ResultAllowedExtensions;
                field.AllowMultipleAttachments = vm.AllowMultipleAttachments;
                field.AttachToEmail = vm.AttachToEmail;
                field.AllowAnyFile = vm.AllowAnyFile;
                if (field.Type == FieldType.Dropdown)
                {
                    field.Options = vm.ResultOptions;
                    field.AutoSelectRules = vm.ResultAutoSelectRules;
                }
                else if (field.Type == FieldType.Calculation)
                {
                    field.ScoringRules = vm.ResultRules;
                }
                else if (field.Type == FieldType.Average)
                {
                    field.TargetIds = vm.ResultAverageIds;
                }

                field.EnableZeroTrigger = vm.ResultEnableZeroTrigger;
                field.ZeroTriggerFieldId = vm.ResultZeroTriggerFieldId;
                field.ZeroTriggerFieldIds = vm.ResultZeroTriggerFieldIds;
                field.ZeroTriggerValue = vm.ResultZeroTriggerValue;
                field.ShowDecimals = vm.ResultShowDecimals;
                field.Rounding = vm.ResultRounding;

                RefreshDashboardFields();
            }
        }

        private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is FieldDefinition f && e.PropertyName == nameof(FieldDefinition.Label))
            {
                var dashboardItem = DashboardFields.FirstOrDefault(d => d.FieldId == f.Id);
                if (dashboardItem != null && dashboardItem.Label != f.Label)
                {
                    dashboardItem.Label = f.Label;
                }
            }
        }

        private void PickDate(FieldDefinition? field)
        {
            if (field == null || field.Type != FieldType.Date) return;
            var selector = new Views.DateSelectorWindow(field.DefaultValue) { Owner = System.Windows.Application.Current.MainWindow };
            if (selector.ShowDialog() == true)
            {
                field.DefaultValue = selector.SelectedValue == "TODAY" ? DateTime.Now.ToString("dd/MM/yyyy") : selector.SelectedValue;
            }
        }

        private void PickTime(FieldDefinition? field)
        {
            if (field == null || field.Type != FieldType.Time) return;
            var selector = new Views.TimeSelectorWindow(field.DefaultValue, field.TimeFormat ?? "HH:mm") { Owner = System.Windows.Application.Current.MainWindow };
            if (selector.ShowDialog() == true)
            {
                field.DefaultValue = selector.SelectedValue == "NOW" ? DateTime.Now.ToString(field.TimeFormat ?? "HH:mm") : selector.SelectedValue;
            }
        }

        private void Cancel()
        {
            CreatedPauta = null;
            OnWizardCanceled?.Invoke(this, new());
        }

        public event EventHandler<EventArgs>? OnWizardCanceled;

        private void AddContact()
        {
            var contact = new RecipientContact { Name = "Nuevo Contacto", Email = "" };
            RecipientContacts.Add(contact);
            EmailNameFieldId = EmailNameFieldId; // Trigger UI update
        }

        private void RemoveContact(RecipientContact? contact)
        {
            if (contact == null) return;
            var result = MessageBox.Show($"¿Eliminar contacto '{contact.Name}'?", "Confirmar", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                RecipientContacts.Remove(contact);
            }
        }
    }

    /// <summary>
    /// Helper model for displaying a field in the dashboard ordering UI (Card 39).
    /// </summary>
    public class FieldDisplayModel : ViewModelBase
    {
        private string _fieldId = string.Empty;
        private string _label = string.Empty;
        private FieldType _type = FieldType.Text;
        private bool _isVisible = true;
        private int _order;

        public string FieldId { get => _fieldId; set => SetProperty(ref _fieldId, value); }
        public string Label { get => _label; set => SetProperty(ref _label, value); }
        public FieldType Type { get => _type; set => SetProperty(ref _type, value); }
        public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
        public int Order { get => _order; set => SetProperty(ref _order, value); }
    }
}