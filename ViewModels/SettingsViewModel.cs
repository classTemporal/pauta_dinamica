using PautaDinamicaApp;
using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Forms; // Using WinForms for FolderBrowserDialog
using System.Collections.ObjectModel;
using System.Linq;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;
using System.Windows;
using System.Windows.Media;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace PautaDinamicaApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;
        private readonly SessionService _sessionService;
        private AppSettings _settings;
        private string _adminPassword = "";
        private bool _isAdminSettingsUnlocked;

        public AppSettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        public ICommand BrowseExcelPathCommand { get; }
        public ICommand BrowseJsonPathCommand { get; }
        public ICommand BrowsePdfPathCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand OpenTemplateManagementCommand { get; }
        public ICommand UnlockAdminSettingsCommand { get; }
        public ICommand PickAccentColorCommand { get; }
        public ICommand OpenHelpCommand { get; }
        public ICommand SwitchUserCommand { get; }
        public ICommand LogoutCommand { get; }

        public UserModel? CurrentUser => SessionService.CurrentUser;
        public string AdminPassword { get => _adminPassword; set => SetProperty(ref _adminPassword, value); }
        public bool IsAdminSettingsUnlocked { get => _isAdminSettingsUnlocked; set => SetProperty(ref _isAdminSettingsUnlocked, value); }

        public bool EnableInternalTimer
        {
            get => Settings.EnableInternalTimer;
            set
            {
                if (Settings.EnableInternalTimer != value)
                {
                    Settings.EnableInternalTimer = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SpellCheckLanguage
        {
            get => Settings.SpellCheckLanguage;
            set
            {
                if (Settings.SpellCheckLanguage != value)
                {
                    Settings.SpellCheckLanguage = value;
                    OnPropertyChanged();
                }
            }
        }

        public Services.AppTheme SelectedTheme
        {
            get => Settings.Theme;
            set
            {
                if (Settings.Theme != value)
                {
                    Settings.Theme = value;
                    OnPropertyChanged();
                    var ts = new ThemeService();
                    ts.SetTheme(value);
                    ts.ApplyAccentColor(Settings.AccentColor);
                }
            }
        }

        public Array Themes => Enum.GetValues(typeof(Services.AppTheme));

        // Popular accent color presets (name → hex)
        public Dictionary<string, string> AccentColors { get; } = new()
        {
            { "Azul", "#007bff" },
            { "Rosa", "#ff6090" },
            { "Rojo", "#dc3545" },
            { "Verde", "#28a745" },
            { "Amarillo", "#ffc107" },
            { "Púrpura", "#6f42c1" },
            { "Teal", "#20c997" },
            { "Naranja", "#fd7e14" }
        };

        private string _selectedAccentColorName = "Azul";
        public string SelectedAccentColorName
        {
            get => _selectedAccentColorName;
            set
            {
                if (_selectedAccentColorName == value) return;
                _selectedAccentColorName = value;
                if (AccentColors.TryGetValue(value, out var hex))
                {
                    Settings.AccentColor = hex;
                    _customAccentColor = "";
                    new ThemeService().ApplyAccentColor(hex);
                    OnPropertyChanged(nameof(SelectedAccentColorName));
                }
            }
        }

        private string _customAccentColor = "";
        public string CustomAccentColor
        {
            get => _customAccentColor;
            set
            {
                if (SetProperty(ref _customAccentColor, value))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        Settings.AccentColor = value;
                        _selectedAccentColorName = "";
                        new ThemeService().ApplyAccentColor(value);
                    }
                }
            }
        }

        public Dictionary<string, string> SpellCheckLanguages { get; } = new()
        {
            { "", "Desactivado" },
            { "es-ES", "Español" },
            { "en-US", "Inglés" },
            { "pt-PT", "Portugués" },
            { "fr-FR", "Francés" }
        };

        private ObservableCollection<PautaSchema> _pautas = new();
        private PautaSchema? _selectedPauta;
        public event Action? RequestClose;

        public ObservableCollection<PautaSchema> Pautas
        {
            get => _pautas;
            set => SetProperty(ref _pautas, value);
        }

        public PautaSchema? SelectedPauta
        {
            get => _selectedPauta;
            set
            {
                if (_selectedPauta == value) return;
                if (_selectedPauta != null)
                {
                    _selectedPauta.PropertyChanged -= OnSelectedPautaPropertyChanged;
                    SyncContacts();
                }

                _selectedPauta = value;
                if (value != null)
                {
                    value.PropertyChanged += OnSelectedPautaPropertyChanged;
                    LoadPautaData(value);
                }

                OnPropertyChanged();
            }
        }

        private void OnSelectedPautaPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectedPauta.EmailNameFieldId))
            {
                // Sync SelectedPautaField so the ComboBox reflects external changes to EmailNameFieldId
                if (!string.IsNullOrEmpty(SelectedPauta?.EmailNameFieldId))
                {
                    SelectedPautaField = CurrentPautaFields.FirstOrDefault(f => f.Id == SelectedPauta.EmailNameFieldId);
                }
                else
                {
                    SelectedPautaField = null;
                }

                if (!string.IsNullOrEmpty(SelectedPauta?.EmailNameFieldId))
                {
                    AutoDetectContactsFromRecords();
                }
                else
                {
                    SyncContacts();
                }
            }
        }

        private ObservableCollection<RecipientContact> _currentContacts = new();
        private ObservableCollection<FieldDefinition> _currentPautaFields = new();
        private FieldDefinition? _selectedPautaField;

        public ObservableCollection<RecipientContact> CurrentContacts { get => _currentContacts; set => SetProperty(ref _currentContacts, value); }
        public ObservableCollection<FieldDefinition> CurrentPautaFields { get => _currentPautaFields; set => SetProperty(ref _currentPautaFields, value); }

        public FieldDefinition? SelectedPautaField
        {
            get => _selectedPautaField;
            set
            {
                if (_selectedPautaField == value) return;
                _selectedPautaField = value;
                OnPropertyChanged();
                if (SelectedPauta != null)
                {
                    SelectedPauta.EmailNameFieldId = value?.Id ?? "";
                    if (value != null) AutoDetectContactsFromRecords();
                    else SyncContacts();
                }
            }
        }

        // Agentes detectados automáticamente que NO tienen correo asociado
        private ObservableCollection<RecipientContact> _missingEmailContacts = new();
        public ObservableCollection<RecipientContact> MissingEmailContacts
        {
            get => _missingEmailContacts;
            set => SetProperty(ref _missingEmailContacts, value);
        }

        private bool _hasMissingEmails;
        public bool HasMissingEmails
        {
            get => _hasMissingEmails;
            set => SetProperty(ref _hasMissingEmails, value);
        }

        public bool ShowNonCriticalMessages
        {
            get => Settings.ShowNonCriticalMessages;
            set
            {
                if (Settings.ShowNonCriticalMessages != value)
                {
                    Settings.ShowNonCriticalMessages = value;
                    OnPropertyChanged();
                }
            }
        }


        public ICommand OpenEmailDirectoryFromWarningCommand { get; }
        public ICommand AddEmailReplacementRuleCommand { get; }
        public ICommand RemoveEmailReplacementRuleCommand { get; }
        public ICommand ToggleEmailRuleMultiSelectCommand { get; }
        public ICommand DeleteSelectedEmailRulesCommand { get; }
        public ICommand SelectAllEmailRulesCommand { get; }
        public ICommand EditEmailRuleFieldsCommand { get; }

        public ICommand PickColorCommand { get; }

        private bool _isEmailRuleMultiSelectMode;
        public bool IsEmailRuleMultiSelectMode { get => _isEmailRuleMultiSelectMode; set => SetProperty(ref _isEmailRuleMultiSelectMode, value); }


        public SettingsViewModel(string activePautaId = "")
        {
            _storageService = new StorageService();
            _sessionService = new SessionService();
            _settings = _storageService.LoadSettings();
            _pautas = new ObservableCollection<PautaSchema>(_storageService.LoadPautas());

            // Seleccionar la pauta activa en Main por defecto si existe
            if (!string.IsNullOrEmpty(activePautaId))
            {
                SelectedPauta = _pautas.FirstOrDefault(p => p.Id == activePautaId) ?? _pautas.FirstOrDefault();
            }
            else
            {
                SelectedPauta = _pautas.FirstOrDefault();
            }

            BrowseExcelPathCommand = new RelayCommand(_ => BrowseFolder(path => Settings.ExcelExportPath = path));
            BrowseJsonPathCommand = new RelayCommand(_ => BrowseFolder(path => Settings.JsonBackupPath = path));
            BrowsePdfPathCommand = new RelayCommand(_ => BrowseFolder(path => Settings.PdfReportPath = path));
            SaveCommand = new RelayCommand(_ => SaveAndClose());
            ApplyCommand = new RelayCommand(_ => SaveSettings(false));
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke());
            PickColorCommand = new RelayCommand(_ => PickColor());
            PickAccentColorCommand = new RelayCommand(_ => PickAccentColor());

            // Initialize accent color selection from saved settings
            InitializeAccentColor();
            ApplyAccentColorToUI(Settings.AccentColor);

            OpenEmailDirectoryFromWarningCommand = new RelayCommand(_ => OpenEmailDirectory());
            OpenTemplateManagementCommand = new RelayCommand(_ => OpenTemplateManagement());
            OpenHelpCommand = new RelayCommand(_ => OpenHelp());
            SwitchUserCommand = new RelayCommand(_ => SwitchUser());
            LogoutCommand = new RelayCommand(_ => Logout());
            UnlockAdminSettingsCommand = new RelayCommand(_ => UnlockAdminSettings());

            AddEmailReplacementRuleCommand = new RelayCommand(_ => AddEmailReplacementRule());
            RemoveEmailReplacementRuleCommand = new RelayCommand(r => RemoveEmailReplacementRule(r as EmailReplacementRule));
            DeleteSelectedEmailRulesCommand = new RelayCommand(_ => DeleteSelectedEmailRules());
            EditEmailRuleFieldsCommand = new RelayCommand(r => EditEmailRuleFields(r as EmailReplacementRule));
            ToggleEmailRuleMultiSelectCommand = new RelayCommand(_ => IsEmailRuleMultiSelectMode = !IsEmailRuleMultiSelectMode);
            SelectAllEmailRulesCommand = new RelayCommand(_ =>
            {
                if (SelectedPauta != null)
                {
                    foreach (var r in SelectedPauta.EmailReplacementRules) r.IsSelected = true;
                }
            });

        }

        private void UnlockAdminSettings()
        {
            if (_sessionService.IsMasterPassword(AdminPassword))
            {
                IsAdminSettingsUnlocked = true;
                AdminPassword = "";
                MessageBoxHelper.Show("Opciones administrativas desbloqueadas.", "Acceso Concedido", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBoxHelper.Show("Contraseña administrativa incorrecta.", "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenTemplateManagement()
        {
            var vm = new TemplateManagementViewModel(SelectedPauta?.Id ?? string.Empty);
            var win = new Views.TemplateManagementWindow { DataContext = vm, Owner = System.Windows.Application.Current.MainWindow };
            win.ShowDialog();
        }

        private void OpenEmailDirectory()
        {
            if (SelectedPauta == null) return;

            var win = new Views.EmailDirectoryWindow { DataContext = this, Owner = System.Windows.Application.Current.MainWindow };
            win.ShowDialog();
        }

        private void LoadPautaData(PautaSchema pauta)
        {
            CurrentContacts = new ObservableCollection<RecipientContact>(pauta.RecipientContacts ?? new());
            CurrentPautaFields = new ObservableCollection<FieldDefinition>(_storageService.LoadConfiguration(pauta.Id).Where(f => f.Type != FieldType.Separator));

            // Sync SelectedPautaField from EmailNameFieldId so the ComboBox SelectedItem binding reflects the current value
            if (!string.IsNullOrEmpty(SelectedPauta?.EmailNameFieldId))
            {
                SelectedPautaField = CurrentPautaFields.FirstOrDefault(f => f.Id == SelectedPauta.EmailNameFieldId);
            }
            else
            {
                SelectedPautaField = null;
            }

            AutoDetectContactsFromRecords();
        }

        private void AutoDetectContactsFromRecords()
        {
            if (SelectedPauta == null || string.IsNullOrEmpty(SelectedPauta.EmailNameFieldId))
            {
                SyncContacts();
                return;
            }

            var selectedField = CurrentPautaFields.FirstOrDefault(f => f.Id == SelectedPauta.EmailNameFieldId);
            var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Priorizar Options del campo (lista de elementos definida por usuario)
            if (selectedField != null && selectedField.Options != null && selectedField.Options.Any())
            {
                foreach (var opt in selectedField.Options)
                {
                    if (!string.IsNullOrWhiteSpace(opt)) uniqueNames.Add(opt.Trim());
                }
            }

            // Fallback: buscar en records guardados
            var records = _storageService.LoadRecords(SelectedPauta.Id);
            foreach (var record in records)
            {
                if (record.Values.TryGetValue(SelectedPauta.EmailNameFieldId, out var val) && val != null)
                {
                    string nameText = val.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrWhiteSpace(nameText)) uniqueNames.Add(nameText);
                }
            }

            var existingMap = CurrentContacts
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .ToDictionary(c => c.Name.Trim(), c => c, StringComparer.OrdinalIgnoreCase);

            var newContacts = new List<RecipientContact>();
            foreach (var name in uniqueNames)
            {
                if (existingMap.TryGetValue(name, out var existing)) newContacts.Add(existing);
                else newContacts.Add(new RecipientContact { Name = name, Email = "" });
            }

            var detectedNames = new HashSet<string>(uniqueNames, StringComparer.OrdinalIgnoreCase);
            foreach (var c in CurrentContacts)
            {
                if (!string.IsNullOrWhiteSpace(c.Name) && !detectedNames.Contains(c.Name.Trim())) newContacts.Add(c);
            }

            CurrentContacts = new ObservableCollection<RecipientContact>(newContacts.OrderBy(c => c.Name).ToList());
            SyncContacts();
        }



        private void AddEmailReplacementRule()
        {
            if (SelectedPauta == null) return;
            var rule = new EmailReplacementRule
            {
                TargetValue = "1",
                ReplacementValue = "Cumple"
            };

            // Pre-select first field if available
            var firstField = CurrentPautaFields.FirstOrDefault();
            if (firstField != null) rule.TargetFieldIds.Add(firstField.Id);

            SelectedPauta.EmailReplacementRules.Add(rule);
        }

        private void RemoveEmailReplacementRule(EmailReplacementRule? rule)
        {
            if (SelectedPauta != null && rule != null)
            {
                if (MessageBoxHelper.ShowNonCritical("¿Eliminar esta regla?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    SelectedPauta.EmailReplacementRules.Remove(rule);
                }
            }
        }

        private void DeleteSelectedEmailRules()
        {
            if (SelectedPauta == null) return;
            var toRemove = SelectedPauta.EmailReplacementRules.Where(r => r.IsSelected).ToList();
            if (toRemove.Count == 0) return;

            if (MessageBoxHelper.ShowNonCritical($"¿Eliminar las {toRemove.Count} reglas seleccionadas?", "Confirmar Eliminación Múltiple", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var r in toRemove) SelectedPauta.EmailReplacementRules.Remove(r);
            }
        }

        private void EditEmailRuleFields(EmailReplacementRule? rule)
        {
            if (rule == null) return;

            // 1. Preparar ViewModels según selección actual
            var selectableFields = CurrentPautaFields.Select(f => new SelectableFieldViewModel
            {
                Id = f.Id,
                Label = f.Label,
                IsSelected = rule.TargetFieldIds.Contains(f.Id)
            }).ToList();

            // 2. Abrir ventana
            var win = new Views.MultiFieldSelectorWindow(selectableFields);
            win.Owner = System.Windows.Application.Current.MainWindow; // Ensure owner is set for centering

            if (win.ShowDialog() == true)
            {
                // 3. Aplicar cambios
                rule.TargetFieldIds.Clear();
                foreach (var sf in selectableFields.Where(x => x.IsSelected))
                {
                    rule.TargetFieldIds.Add(sf.Id);
                }

                // Forzar actualización de UI si es necesario (el PropertyChanged de TargetFieldIds debería bastar)
                // Pero como TargetFieldIds es ObservableCollection, Add dispara CollectionChanged.
            }
        }

        // --- Gestión de Reglas de PDF ---

        private void AddPdfReplacementRule()
        {
            if (SelectedPauta == null) return;
            var rule = new PdfReplacementRule
            {
                TargetValue = "1",
                ReplacementValue = "Cumple",
                TextColor = "#28a745" // Verde por defecto
            };

            var firstField = CurrentPautaFields.FirstOrDefault();
            if (firstField != null) rule.TargetFieldIds.Add(firstField.Id);

            SelectedPauta.PdfReplacementRules.Add(rule);
        }


        private void SyncContacts()
        {
            if (SelectedPauta != null)
            {
                SelectedPauta.RecipientContacts = CurrentContacts.ToList();
            }
            // Refresh missing-email tracking
            var missing = CurrentContacts.Where(c => string.IsNullOrWhiteSpace(c.Email)).ToList();
            MissingEmailContacts = new ObservableCollection<RecipientContact>(missing);
            HasMissingEmails = missing.Any();
        }



 public Array EmailMethods => Enum.GetValues(typeof(EmailMethod));

        private void PickColor()
        {
            if (SelectedPauta == null) return;
            using (var dialog = new ColorDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var c = dialog.Color;
                    SelectedPauta.ColoringColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }
            }
        }

        private void PickAccentColor()
        {
            using (var dialog = new ColorDialog())
            {
                // Set initial color from current Settings.AccentColor
                try
                {
                    if (!string.IsNullOrWhiteSpace(Settings.AccentColor))
                    {
                        var wpfColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Settings.AccentColor);
                        dialog.Color = System.Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
                    }
                }
                catch { /* Use default color in dialog */ }
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var c = dialog.Color;
                    string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                    CustomAccentColor = hex;
                }
            }
        }

        private void InitializeAccentColor()
        {
            // Determine if the saved accent color matches a preset or is custom
            if (!string.IsNullOrWhiteSpace(Settings.AccentColor))
            {
                var match = AccentColors.FirstOrDefault(kvp =>
                    kvp.Value.Equals(Settings.AccentColor, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match.Key))
                {
                    _selectedAccentColorName = match.Key;
                    OnPropertyChanged(nameof(SelectedAccentColorName));
                    _customAccentColor = "";
                }
                else
                {
                    _customAccentColor = Settings.AccentColor;
                    _selectedAccentColorName = "";
                    OnPropertyChanged(nameof(CustomAccentColor));
                    OnPropertyChanged(nameof(SelectedAccentColorName));
                }
            }
        }

        private void ApplyAccentColorToUI(string colorHex)
        {
            new ThemeService().ApplyAccentColor(colorHex);
        }

        private void BrowseFolder(Action<string> updateAction)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    updateAction(dialog.SelectedPath);
                    OnPropertyChanged(nameof(Settings)); // Refresh bindings
                }
            }
        }

        public bool IsSaved { get; private set; }

        private void SaveAndClose()
        {
            SaveSettings(true);
        }

        private void SaveSettings(bool close)
        {
            SyncContacts();

            // Advertencia: contactos sin email
            var missingEmails = CurrentContacts.Where(c => string.IsNullOrWhiteSpace(c.Email)).ToList();
            if (missingEmails.Any())
            {
                var names = string.Join(", ", missingEmails.Select(c => c.Name));
                var result = MessageBoxHelper.Show(
                    $"Los siguientes agentes no tienen correo electrónico asignado:\n{names}\n\n¿Desea guardar de todos modos?",
                    "Advertencia: Correos Faltantes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                    return;
                if (result == MessageBoxResult.No)
                    return;
                // Yes = continuar guardando
            }

            // 1. Validar Pauta Specific Templates
            foreach (var pauta in Pautas)
            {
                var pautaFields = _storageService.LoadConfiguration(pauta.Id);
                var pautaLabels = new HashSet<string>(pautaFields.Select(f => f.Label), StringComparer.OrdinalIgnoreCase);
                pautaLabels.Add("Fecha");

                if (!ValidateTemplateString(pauta.EmailToTemplate, pautaLabels, $"'{pauta.Name}' (Para)", out string err) ||
                    !ValidateTemplateString(pauta.EmailCcTemplate, pautaLabels, $"'{pauta.Name}' (CC)", out err) ||
                    !ValidateTemplateString(pauta.EmailSubjectTemplate, pautaLabels, $"'{pauta.Name}' (Asunto)", out err) ||
                    !ValidateTemplateString(pauta.EmailBodyTemplate, pautaLabels, $"'{pauta.Name}' (Cuerpo)", out err))
                {
                    MessageBoxHelper.Show(err, "Error de Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            _storageService.SaveSettings(Settings);
            _storageService.SavePautas(Pautas.ToList());

            // Apply accent color and theme immediately
            var ts = new ThemeService();
            ts.SetTheme(Settings.Theme);
            ts.ApplyAccentColor(Settings.AccentColor);

            // Sync accent color to the default (login) profile so it persists across sessions
            try
            {
                var defaultStorage = new StorageService("default");
                var defaultSettings = defaultStorage.LoadSettings();
                defaultSettings.AccentColor = Settings.AccentColor;
                defaultSettings.Theme = Settings.Theme;
                defaultStorage.SaveSettings(defaultSettings);
            }
            catch { /* Ignorar error al guardar default */ }

            if (close)
            {
                MessageBoxHelper.ShowNonCritical("Configuración guardada correctamente.", "Éxito");
                IsSaved = true;
                RequestClose?.Invoke();
            }
            else
            {
                MessageBoxHelper.ShowNonCritical("Cambios aplicados correctamente.", "Éxito");
            }
        }

        private bool ValidateTemplateString(string template, HashSet<string> validLabels, string context, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(template)) return true;

            var matches = Regex.Matches(template, @"\[(.*?)\]");
            foreach (Match match in matches)
            {
                string tag = match.Groups[1].Value;
                if (!validLabels.Contains(tag))
                {
                    error = $"La etiqueta '[{tag}]' en el campo {context} no corresponde a ningún campo existente.";
                    return false;
                }
            }
            return true;
        }

        private void OpenHelp()
        {
            var vm = new HelpViewModel("Documentación General", BuildGeneralHelpContent());
            var win = new Views.HelpWindow { DataContext = vm };
            win.Owner = System.Windows.Application.Current.MainWindow;
            win.ShowDialog();
        }

        private void SwitchUser()
        {
            var session = new SessionService();
            session.Logout();

            var loginWin = new Views.LoginWindow();
            loginWin.Show();

            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is Views.SettingsWindow)
                {
                    window.Close();
                    break;
                }
            }
        }

        private void Logout()
        {
            var session = new SessionService();
            session.Logout();

            var loginWin = new Views.LoginWindow();
            loginWin.Show();

            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is Views.SettingsWindow)
                {
                    window.Close();
                    break;
                }
            }
        }

        private static string BuildGeneralHelpContent()
        {
            var content = new System.Text.StringBuilder();
            content.AppendLine("# 📘 Documentación del Sistema");
            content.AppendLine("");
            content.AppendLine("**Versión:** 2.1.0");
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
            return content.ToString();
        }
    }
}
