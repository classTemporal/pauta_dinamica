using PautaDinamicaApp;
using PautaDinamicaApp.Models;
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
using PautaDinamicaApp.Services;
using PautaDinamicaApp.Views;
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
        private AppSettings _settings = new();

        private ICollectionView _recordsView;
        public ICollectionView RecordsView => _recordsView;

        public AppSettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        public ICommand OpenSettingsCommand { get; }
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
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenAttachmentsFolderCommand { get; }
        public ICommand ToggleHeaderCommand { get; }

        public UserModel? CurrentUser => SessionService.CurrentUser;
        private DateTime _currentAuditStartTime = DateTime.Now;

        private DateTime? _filterStartDate;
        public DateTime? FilterStartDate
        {
            get => _filterStartDate;
            set { if (SetProperty(ref _filterStartDate, value)) _recordsView.Refresh(); }
        }

        private DateTime? _filterEndDate;
        public DateTime? FilterEndDate
        {
            get => _filterEndDate;
            set { if (SetProperty(ref _filterEndDate, value)) _recordsView.Refresh(); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) _recordsView.Refresh(); }
        }

        private string _searchFieldId = "ALL";
        public string SearchFieldId
        {
            get => _searchFieldId;
            set 
            { 
                string val = value ?? "ALL";
                if (SetProperty(ref _searchFieldId, val)) 
                {
                    OnPropertyChanged(nameof(SelectedSearchFieldType));
                    OnPropertyChanged(nameof(IsDateFilterVisible));
                    OnPropertyChanged(nameof(IsTimeFilterVisible));
                    _recordsView.Refresh(); 
                }
            }
        }

        public FieldType? SelectedSearchFieldType
        {
            get
            {
                if (SearchFieldId == "ALL") return null;
                if (SearchFieldId == "SYSTEM_TIMESTAMP") return FieldType.Date;
                var field = CurrentFields.FirstOrDefault(f => f.Id == SearchFieldId);
                return field?.Type;
            }
        }

        public bool IsDateFilterVisible => SelectedSearchFieldType == FieldType.Date || SearchFieldId == "SYSTEM_TIMESTAMP";
        public bool IsTimeFilterVisible => SelectedSearchFieldType == FieldType.Time;

        private string _sortFieldId = "SYSTEM_TIMESTAMP";
        public string SortFieldId
        {
            get => _sortFieldId;
            set { if (SetProperty(ref _sortFieldId, value ?? "SYSTEM_TIMESTAMP")) ApplySorting(); }
        }

        private string _sortDirection = "Descendente";
        public string SortDirection
        {
            get => _sortDirection;
            set { if (SetProperty(ref _sortDirection, value)) ApplySorting(); }
        }

        public List<string> SortDirectionOptions => new List<string> { "Ascendente", "Descendente" };

        public bool IsSortAscending
        {
            get => SortDirection == "Ascendente";
            set => SortDirection = value ? "Ascendente" : "Descendente";
        }

        public double DashboardFormWidth
        {
            get => _settings.DashboardFormWidth > 0 ? _settings.DashboardFormWidth : 400;
            set
            {
                if (_settings.DashboardFormWidth != value)
                {
                    _settings.DashboardFormWidth = value;
                    _storageService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(DashboardFormWidth));
                }
            }
        }

        public double DashboardFormHeight
        {
            get => _settings.DashboardFormHeight > 0 ? _settings.DashboardFormHeight : 300;
            set
            {
                if (_settings.DashboardFormHeight != value)
                {
                    _settings.DashboardFormHeight = value;
                    _storageService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(DashboardFormHeight));
                }
            }
        }

        public bool IsFiltersPanelExpanded
        {
            get => _settings.IsFiltersPanelExpanded;
            set
            {
                if (_settings.IsFiltersPanelExpanded != value)
                {
                    _settings.IsFiltersPanelExpanded = value;
                    _storageService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(IsFiltersPanelExpanded));
                }
            }
        }

        public DashboardLayout DashboardLayout
        {
            get => _settings.DashboardLayout;
            set
            {
                if (_settings.DashboardLayout != value)
                {
                    _settings.DashboardLayout = value;
                    _storageService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(DashboardLayout));
                    OnPropertyChanged(nameof(DashboardLayoutString));
                }
            }
        }

        public string DashboardLayoutString
        {
            get => DashboardLayout switch
            {
                DashboardLayout.Left => "Izquierda",
                DashboardLayout.Right => "Derecha",
                DashboardLayout.Top => "Arriba",
                DashboardLayout.Bottom => "Abajo",
                _ => "Izquierda"
            };
            set
            {
                DashboardLayout = value switch
                {
                    "Izquierda" => DashboardLayout.Left,
                    "Derecha" => DashboardLayout.Right,
                    "Arriba" => DashboardLayout.Top,
                    "Abajo" => DashboardLayout.Bottom,
                    _ => DashboardLayout.Left
                };
            }
        }

        public List<string> DashboardLayoutOptions => new List<string> { "Izquierda", "Derecha", "Arriba", "Abajo" };

        public ICommand ToggleFiltersCommand { get; }
        public ICommand ChangeLayoutCommand { get; }
        public ICommand OpenEmailConfigCommand { get; }

        public MainViewModel()
        {
            _storageService = new StorageService();
            _pdfService = new PdfService();
            _emailService = new EmailService();

            // Aplicar tema guardado del usuario al iniciar
            _settings = _storageService.LoadSettings();
            var ts = new ThemeService();
            ts.SetTheme(_settings.Theme);
            ts.ApplyAccentColor(_settings.AccentColor);

            _recordsView = CollectionViewSource.GetDefaultView(_records);
            _recordsView.Filter = FilterRecords;

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
            ExportSelectedCommand = new RelayCommand(_ => ExportRecordsToExcel(Records.Where(r => r.IsSelected).ToList()));
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
            OpenAttachmentsFolderCommand = new RelayCommand(_ => OpenAttachmentsFolder());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            ToggleFiltersCommand = new RelayCommand(_ => IsFiltersPanelExpanded = !IsFiltersPanelExpanded);
            ChangeLayoutCommand = new RelayCommand(_ => RotateLayout());
            OpenEmailConfigCommand = new RelayCommand(_ => OpenEmailConfig());
            ToggleHeaderCommand = new RelayCommand(_ => IsHeaderVisible = !IsHeaderVisible);
            
            // Comandos para Pick Date/Time (mismo comportamiento que en Config)
            PickDateFromCommand = new RelayCommand(p => PickDate(true));
            PickDateToCommand = new RelayCommand(p => PickDate(false));
            PickTimeFromCommand = new RelayCommand(p => PickTime(true));
            PickTimeToCommand = new RelayCommand(p => PickTime(false));

            this.FieldsRefreshed += UpdateFilterOptions;
        }

        private void RotateLayout()
        {
            DashboardLayout = DashboardLayout switch
            {
                DashboardLayout.Left => DashboardLayout.Top,
                DashboardLayout.Top => DashboardLayout.Right,
                DashboardLayout.Right => DashboardLayout.Bottom,
                DashboardLayout.Bottom => DashboardLayout.Left,
                _ => DashboardLayout.Left
            };
        }

        public ICommand PickDateFromCommand { get; }
        public ICommand PickDateToCommand { get; }
        public ICommand PickTimeFromCommand { get; }
        public ICommand PickTimeToCommand { get; }

        private void PickDate(bool isFrom)
        {
            var win = new DateSelectorWindow((isFrom ? FilterStartDate?.ToString("dd/MM/yyyy") : FilterEndDate?.ToString("dd/MM/yyyy")) ?? "");
            win.Owner = System.Windows.Application.Current.MainWindow;
            if (win.ShowDialog() == true)
            {
                if (win.SelectedValue == "TODAY")
                {
                    if (isFrom) FilterStartDate = DateTime.Today;
                    else FilterEndDate = DateTime.Today.AddHours(23).AddMinutes(59);
                }
                else if (DateTime.TryParse(win.SelectedValue, out DateTime date))
                {
                    if (isFrom) FilterStartDate = date;
                    else FilterEndDate = date.AddHours(23).AddMinutes(59);
                }
                else
                {
                    if (isFrom) FilterStartDate = null;
                    else FilterEndDate = null;
                }
            }
        }

        private void PickTime(bool isFrom)
        {
            var win = new TimeSelectorWindow(isFrom ? (FilterStartDate?.ToString("HH:mm") ?? "00:00") : (FilterEndDate?.ToString("HH:mm") ?? "23:59"));
            win.Owner = System.Windows.Application.Current.MainWindow;
            if (win.ShowDialog() == true)
            {
                if (win.SelectedValue == "NOW")
                {
                    if (isFrom) FilterStartDate = DateTime.Today.Add(DateTime.Now.TimeOfDay);
                    else FilterEndDate = DateTime.Today.Add(DateTime.Now.TimeOfDay);
                }
                else if (DateTime.TryParse(win.SelectedValue, out DateTime time))
                {
                    // Usar hoy como fecha base para el filtro de tiempo
                    DateTime baseDate = DateTime.Today;
                    if (isFrom) FilterStartDate = baseDate.Add(time.TimeOfDay);
                    else FilterEndDate = baseDate.Add(time.TimeOfDay);
                }
                else
                {
                    if (isFrom) FilterStartDate = null;
                    else FilterEndDate = null;
                }
            }
        }

        private void UpdateFilterOptions()
        {
            OnPropertyChanged(nameof(FilterFieldOptions));
            OnPropertyChanged(nameof(SortFieldOptions));
            
            // Reset to default if current selection is not valid anymore
            if (!FilterFieldOptions.Any(o => o.Id == SearchFieldId)) SearchFieldId = "ALL";
            if (!SortFieldOptions.Any(o => o.Id == SortFieldId)) SortFieldId = "SYSTEM_TIMESTAMP";
        }

        public class FieldOption
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
        }

        public List<FieldOption> FilterFieldOptions
        {
            get
            {
                var options = new List<FieldOption> { new FieldOption { Id = "ALL", Name = "Todos los campos" } };
                options.AddRange(CurrentFields.Where(f => f.Type != FieldType.Separator)
                                             .Select(f => new FieldOption { Id = f.Id, Name = f.Label }));
                return options;
            }
        }

        public List<FieldOption> SortFieldOptions
        {
            get
            {
                var options = new List<FieldOption> { new FieldOption { Id = "SYSTEM_TIMESTAMP", Name = "Fecha de Creación" } };
                options.AddRange(CurrentFields.Where(f => f.Type != FieldType.Separator)
                                             .Select(f => new FieldOption { Id = f.Id, Name = f.Label }));
                return options;
            }
        }

        private void ClearFilters()
        {
            FilterStartDate = null;
            FilterEndDate = null;
            SearchText = string.Empty;
            SearchFieldId = "ALL";
        }

        private bool FilterRecords(object obj)
        {
            if (obj is not AuditEntry entry) return false;

            // Filtro por Búsqueda (en todos o campo específico)
            string search = SearchText?.ToLower() ?? "";
            
            if (SearchFieldId == "ALL")
            {
                // En modo global, el rango de fechas afecta al Timestamp
                if (FilterStartDate.HasValue && entry.Timestamp < FilterStartDate.Value) return false;
                if (FilterEndDate.HasValue && entry.Timestamp > FilterEndDate.Value) return false;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    bool found = entry.Timestamp.ToString().ToLower().Contains(search);
                    if (!found)
                    {
                        foreach (var val in entry.Values.Values)
                        {
                            if (val?.ToString()?.ToLower().Contains(search) == true)
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found) return false;
                }
            }
            else
            {
                // Buscar solo en el campo seleccionado
                if (!string.IsNullOrEmpty(SearchFieldId) && entry.Values.TryGetValue(SearchFieldId, out var val))
                {
                    string strVal = val?.ToString() ?? "";
                    
                    if (IsDateFilterVisible || IsTimeFilterVisible)
                    {
                        if (DateTime.TryParse(strVal, out DateTime fieldDt))
                        {
                            if (IsTimeFilterVisible)
                            {
                                // Solo comparar horas
                                TimeSpan t = fieldDt.TimeOfDay;
                                if (FilterStartDate.HasValue && t < FilterStartDate.Value.TimeOfDay) return false;
                                if (FilterEndDate.HasValue && t > FilterEndDate.Value.TimeOfDay) return false;
                            }
                            else
                            {
                                if (FilterStartDate.HasValue && fieldDt.Date < FilterStartDate.Value.Date) return false;
                                if (FilterEndDate.HasValue && fieldDt.Date > FilterEndDate.Value.Date) return false;
                            }
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(search) && !strVal.ToLower().Contains(search)) return false;
                }
                else if (SearchFieldId == "SYSTEM_TIMESTAMP")
                {
                    if (FilterStartDate.HasValue && entry.Timestamp < FilterStartDate.Value) return false;
                    if (FilterEndDate.HasValue && entry.Timestamp > FilterEndDate.Value) return false;
                    
                    if (!string.IsNullOrWhiteSpace(search) && !entry.Timestamp.ToString().ToLower().Contains(search)) return false;
                }
                else
                {
                    // Si el campo no existe en el registro y no es global, ocultar (a menos que no haya filtros)
                    if (!string.IsNullOrWhiteSpace(search) || FilterStartDate.HasValue || FilterEndDate.HasValue) return false;
                }
            }

            return true;
        }

        private void ApplySorting()
        {
            var view = _recordsView as ListCollectionView;
            if (view == null) return;

            view.CustomSort = new AuditEntryComparer(SortFieldId, IsSortAscending);
        }

        public class AuditEntryComparer : System.Collections.IComparer
        {
            private readonly string _fieldId;
            private readonly bool _ascending;

            public AuditEntryComparer(string fieldId, bool ascending)
            {
                _fieldId = fieldId;
                _ascending = ascending;
            }

            public int Compare(object? x, object? y)
            {
                if (x is not AuditEntry a || y is not AuditEntry b) return 0;

                int result = 0;
                if (_fieldId == "SYSTEM_TIMESTAMP")
                {
                    result = DateTime.Compare(a.Timestamp, b.Timestamp);
                }
                else
                {
                    string valA = !string.IsNullOrEmpty(_fieldId) && a.Values.ContainsKey(_fieldId) ? a.Values[_fieldId]?.ToString() ?? "" : "";
                    string valB = !string.IsNullOrEmpty(_fieldId) && b.Values.ContainsKey(_fieldId) ? b.Values[_fieldId]?.ToString() ?? "" : "";

                    // Intentar comparación numérica si ambos son números o porcentajes
                    if (IsNumericOrPercentage(valA) && IsNumericOrPercentage(valB))
                    {
                        double d1 = ParsePercentage(valA);
                        double d2 = ParsePercentage(valB);
                        result = d1.CompareTo(d2);
                    }
                    else
                    {
                        result = string.Compare(valA, valB, StringComparison.OrdinalIgnoreCase);
                    }
                }

                return _ascending ? result : -result;
            }

            private bool IsNumericOrPercentage(string val)
            {
                if (string.IsNullOrWhiteSpace(val)) return false;
                string clean = val.Replace("%", "").Trim();
                return double.TryParse(clean, out _);
            }

            private double ParsePercentage(string val)
            {
                if (string.IsNullOrWhiteSpace(val)) return 0;
                string clean = val.Replace("%", "").Trim();
                if (double.TryParse(clean, out double d)) return d;
                return 0;
            }
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

            // Also ensure accent color persists on the default profile
            var ts = new ThemeService();
            ts.SetTheme(newTheme);
            ts.ApplyAccentColor(settings.AccentColor);
            try
            {
                var defaultStorage = new StorageService("default");
                var defaultSettings = defaultStorage.LoadSettings();
                defaultSettings.Theme = newTheme;
                defaultSettings.AccentColor = settings.AccentColor;
                defaultStorage.SaveSettings(defaultSettings);
            }
            catch { /* Ignorar error al guardar default */ }
        }

        private void OpenAttachmentsFolder()
        {
            if (CurrentPauta == null) return;
            string path = _storageService.GetPautaAttachmentsDir(CurrentPauta.Id);
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            else
            {
                MessageBoxHelper.Show("Aún no hay archivos adjuntos para esta pauta.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
            var vm = new SettingsViewModel(CurrentPauta?.Id ?? "");
            var settingsWin = new Views.SettingsWindow { DataContext = vm };
            var owner = GetBestOwner();
            if (owner != null && owner != settingsWin) settingsWin.Owner = owner;

            vm.RequestClose += () => settingsWin.Close();
            settingsWin.ShowDialog();

            if (vm.IsSaved)
            {
                Settings = vm.Settings;
                LoadPautas();
                ApplyRowColoring();
                UpdateAuditStats();
            }
        }

        private void OpenEmailConfig()
        {
            var vm = new SettingsViewModel(CurrentPauta?.Id ?? "");
            var settingsWin = new Views.SettingsWindow { DataContext = vm, InitialTabIndex = 4 };
            var owner = GetBestOwner();
            if (owner != null && owner != settingsWin) settingsWin.Owner = owner;

            vm.RequestClose += () => settingsWin.Close();
            settingsWin.ShowDialog();

            if (vm.IsSaved)
            {
                Settings = vm.Settings;
                LoadPautas();
                ApplyRowColoring();
                UpdateAuditStats();
            }
        }

        private void ApplyRowColoring()
        {
            if (CurrentPauta == null) return;

            string targetFieldLabel = CurrentPauta.ColoringField;
            string targetValue = CurrentPauta.ColoringValue;
            string targetColor = CurrentPauta.ColoringColor;

            if (string.IsNullOrWhiteSpace(targetFieldLabel) || string.IsNullOrWhiteSpace(targetValue))
            {
                // Limpiar colores si no hay regla
                foreach (var r in Records) r.RowColor = null;
                return;
            }

            // Buscar ID del campo basado en el Label (Nombre)
            var fields = _storageService.LoadConfiguration(CurrentPauta.Id);
            var targetField = fields.FirstOrDefault(f => f.Label.Equals(targetFieldLabel, StringComparison.OrdinalIgnoreCase));

            if (targetField == null)
            {
                foreach (var r in Records) r.RowColor = null;
                return; // Campo no encontrado
            }

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

        public bool ExportRecordsToExcel(IEnumerable<AuditEntry>? recordsToExport = null, string? customTitle = null, bool silent = false)
        {
            var data = (recordsToExport ?? Records).ToList();
            if (!data.Any()) return false;

            List<ExportColumnConfig>? allConfig = null;
            string selectedPresetName = "";
            if (CurrentPauta != null)
            {
                if (CurrentPauta.ExportPresets != null && CurrentPauta.ExportPresets.Count > 1 && !silent)
                {
                    var dialog = new ExportPresetSelectionWindow(CurrentPauta.ExportPresets);
                    dialog.Owner = System.Windows.Application.Current.MainWindow;
                    if (dialog.ShowDialog() == true)
                    {
                        allConfig = dialog.SelectedPreset?.Columns;
                        selectedPresetName = dialog.SelectedPreset?.Name ?? "";
                    }
                    else
                    {
                        return false; // Usuario canceló la selección
                    }
                }
                else
                {
                    // Usar el único preset disponible, o caer en el config legacy si no hay presets
                    var preset = CurrentPauta.ExportPresets?.FirstOrDefault();
                    allConfig = preset?.Columns ?? CurrentPauta.ExportConfig;
                    selectedPresetName = preset?.Name ?? "";
                }
            }

            // --- CONSTRUIR NOMBRE DE ARCHIVO ---
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
                string safePautaName = string.Join("_", pautaName.Split(Path.GetInvalidFileNameChars())).Trim().Replace(" ", "_");

                string? baseName = customTitle;
                if (string.IsNullOrEmpty(baseName))
                {
                    baseName = (recordsToExport != null ? "Parcial_" : "") + safePautaName;
                }

                string presetPart = !string.IsNullOrEmpty(selectedPresetName) ? $"_{selectedPresetName}" : "";
                string fileName = $"{baseName}{presetPart}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                filePath = Path.Combine(exportDir, fileName);
            }

            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Auditoría");

                    // --- DEFINIR COLUMNAS A EXPORTAR ---
                    var columnsToExport = new List<(string FieldId, string Header, FieldType Type, FieldDefinition? Def)>();
                    var globalSettings = _storageService.LoadSettings();

                    if (allConfig == null) allConfig = new System.Collections.Generic.List<ExportColumnConfig>();
                    var exportConfig = allConfig.Where(c => c.IsExportEnabled).OrderBy(c => c.Order).ToList();

                    if (exportConfig != null && exportConfig.Any())
                    {
                        if (globalSettings.EnableInternalTimer)
                        {
                            columnsToExport.Add(("System_Duration", "Duración (min)", FieldType.Numeric, null));
                        }

                        foreach (var config in exportConfig)
                        {
                            if (config.FieldId == "System_Timestamp") continue;
                            if (config.FieldId == "System_Duration") continue;

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
                        if (globalSettings.EnableInternalTimer)
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
                                else if (type == FieldType.FileAttachment)
                                {
                                    var paths = strVal.Split('|').Where(p => !string.IsNullOrEmpty(p)).ToList();
                                    if (paths.Any())
                                    {
                                        var missing = paths.Where(p => !File.Exists(p)).ToList();
                                        if (missing.Any())
                                        {
                                            cell.Value = "Archivo adjunto perdido";
                                            cell.Style.Font.FontColor = XLColor.Red;
                                        }
                                        else
                                        {
                                            cell.Value = string.Join(", ", paths);
                                        }
                                    }
                                    else cell.Value = "";
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
                if (!silent) MessageBoxHelper.ShowNonCritical($"Exportación a Excel exitosa en:\n{filePath}", "Éxito");
                return true;
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Show($"Error al exportar Excel: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                if (MessageBoxHelper.ShowNonCritical($"Exportación a JSON exitosa.\n\nArchivo guardado en:\n{filePath}\n\n¿Desea abrir la carpeta ahora?", "Éxito", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    if (Directory.Exists(exportDir)) System.Diagnostics.Process.Start("explorer.exe", exportDir);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Show($"Error al exportar JSON: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            MessageBoxHelper.Show("No se pudieron mapear las columnas. Asegúrate de que los nombres de cabecera coincidan con la Pauta actual.", "Error Importación", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        MessageBoxHelper.ShowNonCritical($"Importación completada. Se importaron {importedCount} registros.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        RefreshCalculations();
                    }
                }
                catch (Exception ex)
                {
                    MessageBoxHelper.Show($"Error al importar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                int count = 0;

                foreach (var record in records)
                {
                    if (GeneratePdfCommon(new List<AuditEntry> { record }, silent: true) != null)
                    {
                        count++;
                    }
                }

                if (MessageBoxHelper.ShowNonCritical($"Se generaron {count} PDFs en:\n{folderPath}\n\n¿Abrir carpeta?", "Éxito", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Show($"Error al generar PDFs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // Evitar colisiones de archivos y bloqueos (sobre todo en envíos masivos)
                if (File.Exists(filePath))
                {
                    string directory = Path.GetDirectoryName(filePath) ?? exportDir;
                    string nameOnly = Path.GetFileNameWithoutExtension(filePath);
                    string extension = Path.GetExtension(filePath);
                    int counter = 1;
                    while (File.Exists(filePath))
                    {
                        filePath = Path.Combine(directory, $"{nameOnly}_{counter++}{extension}");
                    }
                }

                _pdfService.GenerateAuditPdf(records, definitions, CurrentPauta?.PdfConfig, pautaName, filePath, CurrentPauta?.PdfReplacementRules?.ToList());

                if (!silent)
                {
                    if (MessageBoxHelper.ShowNonCritical($"PDF Generado con éxito en:\n{filePath}\n\n¿Abrir ahora?", "Éxito", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true } }.Start();
                    }
                }
                return filePath;
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Show($"Error al generar PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            set 
            { 
                if (SetProperty(ref _records, value))
                {
                    _recordsView = CollectionViewSource.GetDefaultView(_records);
                    _recordsView.Filter = FilterRecords;
                    ApplySorting();
                    OnPropertyChanged(nameof(RecordsView));
                }
            }
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

        private bool _isHeaderVisible = true;
        public bool IsHeaderVisible
        {
            get => _isHeaderVisible;
            set { if (SetProperty(ref _isHeaderVisible, value)) { /* visibility change triggers via binding */ } }
        }

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
            
            // Reutilizar la colección si es posible o disparar el setter
            Records = new ObservableCollection<AuditEntry>(savedRecords);
            
            Records.CollectionChanged += (s, e) => UpdateAuditStats();
            UpdateAuditStats();

            ApplyRowColoring();
            ValidateAllRecordAttachments();
            ApplySorting(); // Asegurar que el orden se aplique al cargar
            FieldsRefreshed?.Invoke();
            CreateNewRecord();
        }

        private void ValidateAllRecordAttachments()
        {
            if (CurrentPauta == null || Records == null) return;
            var config = _storageService.LoadConfiguration(CurrentPauta.Id);
            var attachmentFields = config.Where(f => f.Type == FieldType.FileAttachment).ToList();

            foreach (var record in Records)
            {
                bool missing = false;
                foreach (var field in attachmentFields)
                {
                    if (record.Values.TryGetValue(field.Id, out var val) && val != null)
                    {
                        string strVal = val.ToString() ?? "";
                        var paths = strVal.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var path in paths)
                        {
                            if (!File.Exists(path))
                            {
                                missing = true;
                                break;
                            }
                        }
                    }
                    if (missing) break;
                }
                record.HasMissingAttachments = missing;
            }
        }

        public void RefreshFields()
        {
            if (CurrentPauta == null) return;
            var config = _storageService.LoadConfiguration(CurrentPauta.Id).OrderBy(f => f.Order).ToList();
            
            // Card 39: Apply DashboardFieldOrder from PautaSchema if available
            // This allows the dashboard to display fields in a different order than the main config list
            if (CurrentPauta.DashboardFieldOrder != null && CurrentPauta.DashboardFieldOrder.Any())
            {
                var orderedConfig = new List<FieldDefinition>();
                var orderedIds = new HashSet<string>(CurrentPauta.DashboardFieldOrder);
                
                // First, add fields in the dashboard-specified order
                foreach (var fieldId in CurrentPauta.DashboardFieldOrder)
                {
                    var field = config.FirstOrDefault(f => f.Id == fieldId);
                    if (field != null) orderedConfig.Add(field);
                }
                
                // Then, add any remaining fields that weren't in the dashboard order
                foreach (var field in config)
                {
                    if (!orderedIds.Contains(field.Id))
                    {
                        orderedConfig.Add(field);
                    }
                }
                
                config = orderedConfig;
            }
            
            var fields = config.Where(c => c.Type != FieldType.Separator).Select(c => new DynamicFieldVM(c)).ToList();

            foreach (var f in fields)
            {
                f.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(DynamicFieldVM.Value) && !_isCalculating)
                    {
                        RefreshCalculations();
                        CheckDuplicateWarning(f);
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

        /// <summary>
        /// Persists the current dashboard field order into CurrentPauta.DashboardFieldOrder
        /// and saves via StorageService.
        /// </summary>
        public void SaveDashboardFieldOrder()
        {
            if (CurrentPauta == null) return;
            CurrentPauta.DashboardFieldOrder = CurrentFields.Select(f => f.Id).ToList();
            _storageService.SavePautas(Pautas.ToList());
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
                                        else if (triggerVal.Equals("True", StringComparison.OrdinalIgnoreCase)) triggerVal = "1";
                                        else if (triggerVal.Equals("False", StringComparison.OrdinalIgnoreCase)) triggerVal = "0";
                                    }

                                    if (EvaluateRule(triggerVal, "=", def.ZeroTriggerValue))
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
                                        else if (triggerVal.Equals("True", StringComparison.OrdinalIgnoreCase)) triggerVal = "1";
                                        else if (triggerVal.Equals("False", StringComparison.OrdinalIgnoreCase)) triggerVal = "0";
                                    }

                                    if (EvaluateRule(triggerVal, "=", def.ZeroTriggerValue))
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

            // 1. Comparación Directa (Ignorando mayúsculas/minúsculas)
            if (string.Equals(s, r, StringComparison.OrdinalIgnoreCase)) return true;

            // 2. Normalización Booleana (Mapear equivalencias comunes)
            if (op == "=")
            {
                string sLower = s.ToLower();
                string rLower = r.ToLower();

                bool? sBool = sLower == "1" || sLower == "true" || sLower == "si" || sLower == "sí" ? true :
                             (sLower == "0" || sLower == "false" || sLower == "no" ? false : (bool?)null);

                bool? rBool = rLower == "1" || rLower == "true" || rLower == "si" || rLower == "sí" ? true :
                             (rLower == "0" || rLower == "false" || rLower == "no" ? false : (bool?)null);

                if (sBool.HasValue && rBool.HasValue) return sBool == rBool;
            }

            // 3. Comparación Numérica (Robusta con decimales y separadores)
            if (double.TryParse(s.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sNum) &&
                double.TryParse(r.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double rNum))
            {
                if (op == "=") return Math.Abs(sNum - rNum) < 0.0001;
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

            var editorVm = win.DataContext as EditorViewModel;
            bool? result = win.ShowDialog();

            if (result == true || (editorVm != null && editorVm.WasDatabaseModified))
            {
                LoadPautas(); // Recargar lista por si se agregaron/eliminaron pautas
                
                if (editorVm != null && editorVm.ShouldClearRecords)
                {
                    // Los respaldos ya se hicieron dentro del ConfigWindow.
                    // Aquí solo limpiamos y refrescamos la vista actual del MainViewModel.
                    Records.Clear();
                    RefreshFields();
                    MessageBoxHelper.ShowNonCritical("La vista se ha refrescado debido a cambios estructurales o restauración de base de datos.", "Éxito");
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

        /// <summary>
        /// Checks the current value of a field against all existing records in real-time
        /// and sets a visible warning on the field if a duplicate is detected.
        /// </summary>
        private void CheckDuplicateWarning(DynamicFieldVM field)
        {
            // Clear any previous warning first
            field.DuplicateWarning = "";

            var def = field.Definition;
            bool checkType = def.Type == FieldType.Text || def.Type == FieldType.Numeric || def.Type == FieldType.TextArea;

            // Only check if the feature is enabled on this field and there is a value
            if (!checkType || !def.WarnOnDuplicate)
                return;

            string strValue = field.Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(strValue))
                return;

            string currentValue = strValue.Trim();

            // Search for the same value in other existing records (exclude the one being edited)
            bool isDuplicate = Records.Any(r =>
                r != SelectedRecord &&
                r.Values.TryGetValue(field.Definition.Id, out var val) &&
                val != null &&
                string.Equals(val.ToString()!.Trim(), currentValue, StringComparison.OrdinalIgnoreCase));

            if (isDuplicate)
            {
                field.DuplicateWarning = $"⚠️ Valor duplicado: '{currentValue}' ya existe en otro registro.";
            }
        }

        private void SaveCurrentRecord()
        {
            foreach (var field in CurrentFields) field.Validate();
            if (CurrentFields.Any(f => !f.IsValid))
            {
                string errors = string.Join("\n", CurrentFields.Where(f => !f.IsValid).Select(f => $"- {f.Label}: {f.ValidationError}"));
                MessageBoxHelper.Show($"Por favor, corrija los siguientes errores:\n\n{errors}", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        var result = MessageBoxHelper.Show(
                                                    $"El valor '{currentValue}' en el campo '{field.Label}' ya existe en otro registro.\n\n¿Desea agregarlo de todas formas?",
                                                    "Valor Duplicado Detectado",
                                                    MessageBoxButton.YesNo,
                                                    MessageBoxImage.Warning, true);

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
            ValidateAllRecordAttachments();

            // Reiniciar todo para la siguiente auditoría (limpia campos y resetea el temporizador)
            CreateNewRecord();

            MessageBoxHelper.ShowNonCritical("Registro guardado correctamente.", "Éxito");
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
            if (MessageBoxHelper.ShowNonCritical("¿Eliminar registro?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Records.Remove(entry);
                if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
                if (SelectedRecord == entry) CreateNewRecord();
            }
        }

        private void DeleteAllRecords()
        {
            if (MessageBoxHelper.Show("¿Eliminar TODOS los registros de esta pauta?", "Confirmar Eliminación Total", MessageBoxButton.YesNo, MessageBoxImage.Warning, true) == MessageBoxResult.Yes)
            {
                bool wasEditing = SelectedRecord != null;
                Records.Clear();
                if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
                if (wasEditing) CreateNewRecord();
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
                MessageBoxHelper.ShowNonCritical("No hay registros seleccionados.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selected.Count == 1) GeneratePdfCommon(selected);
            else GenerateBatchPdfs(selected);
        }

        private async void SendEmails(AuditEntry? singleEntry = null)
        {
            if (CurrentPauta == null)
            {
                MessageBoxHelper.Show("No hay una pauta activa.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBoxHelper.ShowNonCritical("No hay registros para enviar.", "Aviso");
                return;
            }

            // Aplicar lógica de exclusión por calificación/campo valor
            // Funciona tanto para envío individual como múltiple.
            if (!string.IsNullOrEmpty(CurrentPauta.ExcludeByFieldId) && !string.IsNullOrEmpty(CurrentPauta.ExcludeByFieldValue))
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
                    var res = MessageBoxHelper.Show($"Se han excluido {excluded} registros según la regla de la pauta.\n\n¿Desea continuar con los {toProcess.Count} restantes?", "Filtro de Exclusión", MessageBoxButton.YesNo, MessageBoxImage.Question, true);
                    if (res == MessageBoxResult.No) return;
                }
            }

            // --- VALIDACIÓN DE CORREOS FALTANTES (Card 31) ---
            // Si el envío es automático (UseAutomatedRecipient) y hay agentes sin correo asociado,
            // bloquear el envío y mostrar un mensaje con opción de ir al directorio de contactos.
            if (CurrentPauta.UseAutomatedRecipient && !string.IsNullOrEmpty(CurrentPauta.EmailNameFieldId))
            {
                var contacts = CurrentPauta.RecipientContacts ?? new List<RecipientContact>();
                var missingEmailAgents = new List<string>();

                foreach (var entry in toProcess)
                {
                    if (entry.Values.TryGetValue(CurrentPauta.EmailNameFieldId, out var nameVal) && nameVal != null)
                    {
                        string nameText = nameVal.ToString()?.Trim() ?? "";
                        if (string.IsNullOrWhiteSpace(nameText)) continue;

                        var contact = contacts.FirstOrDefault(c => string.Equals(c.Name?.Trim(), nameText, StringComparison.OrdinalIgnoreCase));
                        if (contact == null || string.IsNullOrWhiteSpace(contact.Email))
                        {
                            missingEmailAgents.Add(nameText);
                        }
                    }
                }

                if (missingEmailAgents.Any())
                {
                    string agentList = string.Join("\n", missingEmailAgents.Distinct().Select(a => $"• {a}"));
                    string msg = $"No se puede enviar el correo porque los siguientes agentes no tienen correo electrónico asociado:\n\n{agentList}\n\n" +
                                 "Diríjase al Directorio de Contactos para completar los correos.\n\n¿Abrir Directorio de Contactos ahora?";

                    var result = MessageBoxHelper.Show(msg, "Correos Faltantes", MessageBoxButton.YesNo, MessageBoxImage.Warning, true);
                    if (result == MessageBoxResult.Yes)
                    {
                        // Abrir la ventana de Configuración > pestaña Correo > Directorio de Contactos
                        var settingsVm = new ViewModels.SettingsViewModel(CurrentPauta.Id);
                        var settingsWin = new Views.SettingsWindow { DataContext = settingsVm, Owner = System.Windows.Application.Current.MainWindow };
                        settingsVm.RequestClose += () => settingsWin.Close();
                        settingsWin.ShowDialog();
                    }
                    return; // Bloquear el envío
                }
            }

            if (toProcess.Count > 1)
            {
                var confirm = MessageBoxHelper.ShowNonCritical($"Se prepararán {toProcess.Count} correos individuales. ¿Continuar?", "Confirmar Envío", MessageBoxButton.YesNo);
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

                    // Añadir un pequeño retraso para evitar que Windows ignore las peticiones (especialmente con Mailto)
                    if (toProcess.Count > 1)
                    {
                        await System.Threading.Tasks.Task.Delay(800);
                    }
                }
                catch (Exception ex)
                {
                    MessageBoxHelper.Show($"Error al exportar Excel: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                if (MessageBoxHelper.ShowNonCritical(msg + "\n\n¿Desea abrir la carpeta de los reportes ahora?", "Proceso Finalizado", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
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
            if (MessageBoxHelper.ShowNonCritical($"¿Eliminar {selected.Count}?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                bool wasEditingDeleted = SelectedRecord != null && selected.Contains(SelectedRecord);
                foreach (var rec in selected) Records.Remove(rec);
                if (CurrentPauta != null) _storageService.SaveRecords(CurrentPauta.Id, Records.ToList());
                if (wasEditingDeleted) CreateNewRecord();
            }
        }

        // End of MainViewModel
    }
}
