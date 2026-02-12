using System;
using System.Windows.Input;
using System.Windows.Forms; // Using WinForms for FolderBrowserDialog
using System.Collections.ObjectModel;
using System.Linq;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;
using System.Windows;
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
        public ICommand CancelCommand { get; }
        public ICommand OpenTemplateManagementCommand { get; }
        public ICommand UnlockAdminSettingsCommand { get; }

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
                if (_selectedPauta != null) SyncContacts();

                _selectedPauta = value;
                if (value != null)
                {
                    LoadPautaData(value);
                }

                OnPropertyChanged();
            }
        }

        private ObservableCollection<RecipientContact> _currentContacts = new();
        private ObservableCollection<FieldDefinition> _currentPautaFields = new();
        private bool _allContactsSelected;

        public ObservableCollection<RecipientContact> CurrentContacts { get => _currentContacts; set => SetProperty(ref _currentContacts, value); }
        public ObservableCollection<FieldDefinition> CurrentPautaFields { get => _currentPautaFields; set => SetProperty(ref _currentPautaFields, value); }

        public bool AllContactsSelected
        {
            get => _allContactsSelected;
            set
            {
                if (SetProperty(ref _allContactsSelected, value))
                {
                    foreach (var c in CurrentContacts) c.IsSelected = value;
                }
            }
        }

        public ICommand AddContactCommand { get; }
        public ICommand DeleteContactCommand { get; }
        public ICommand ImportContactsCommand { get; }
        public ICommand ExportContactsCommand { get; }
        public ICommand SelectAllContactsCommand { get; }
        public ICommand DeleteSelectedContactsCommand { get; }
        public ICommand OpenEmailDirectoryCommand { get; }
        public ICommand ToggleContactMultiSelectCommand { get; }
        public ICommand PickColorCommand { get; }

        private bool _isContactMultiSelectMode;
        public bool IsContactMultiSelectMode { get => _isContactMultiSelectMode; set => SetProperty(ref _isContactMultiSelectMode, value); }

        public SettingsViewModel()
        {
            _storageService = new StorageService();
            _sessionService = new SessionService();
            _settings = _storageService.LoadSettings();
            _pautas = new ObservableCollection<PautaSchema>(_storageService.LoadPautas());
            SelectedPauta = _pautas.FirstOrDefault();

            BrowseExcelPathCommand = new RelayCommand(_ => BrowseFolder(path => Settings.ExcelExportPath = path));
            BrowseJsonPathCommand = new RelayCommand(_ => BrowseFolder(path => Settings.JsonBackupPath = path));
            BrowsePdfPathCommand = new RelayCommand(_ => BrowseFolder(path => Settings.PdfReportPath = path));
            SaveCommand = new RelayCommand(_ => SaveAndClose());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke());
            PickColorCommand = new RelayCommand(_ => PickColor());

            AddContactCommand = new RelayCommand(_ => AddContact());
            DeleteContactCommand = new RelayCommand(p => DeleteContact(p as RecipientContact));
            ImportContactsCommand = new RelayCommand(_ => ImportContacts());
            ExportContactsCommand = new RelayCommand(_ => ExportContacts());
            DeleteSelectedContactsCommand = new RelayCommand(_ => DeleteSelectedContacts());
            SelectAllContactsCommand = new RelayCommand(p => { AllContactsSelected = (bool)(p ?? false); });
            OpenEmailDirectoryCommand = new RelayCommand(_ => OpenEmailDirectory());
            ToggleContactMultiSelectCommand = new RelayCommand(_ => IsContactMultiSelectMode = !IsContactMultiSelectMode);
            OpenTemplateManagementCommand = new RelayCommand(_ => OpenTemplateManagement());
            UnlockAdminSettingsCommand = new RelayCommand(_ => UnlockAdminSettings());
        }

        private void UnlockAdminSettings()
        {
            if (_sessionService.IsMasterPassword(AdminPassword))
            {
                IsAdminSettingsUnlocked = true;
                AdminPassword = "";
                System.Windows.MessageBox.Show("Opciones administrativas desbloqueadas.", "Acceso Concedido", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("Contraseña administrativa incorrecta.", "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenTemplateManagement()
        {
            var vm = new TemplateManagementViewModel();
            var win = new Views.TemplateManagementWindow { DataContext = vm, Owner = System.Windows.Application.Current.MainWindow };
            win.ShowDialog();
        }

        private void OpenEmailDirectory()
        {
            if (SelectedPauta == null) return;

            // Crear respaldo para Cancelar
            var backupContacts = CurrentContacts.Select(c => new RecipientContact { Name = c.Name, Email = c.Email }).ToList();
            var backupFieldId = SelectedPauta.EmailNameFieldId;

            var win = new Views.EmailDirectoryWindow { DataContext = this, Owner = System.Windows.Application.Current.MainWindow };
            var result = win.ShowDialog();

            if (result != true)
            {
                // Restaurar respaldo
                CurrentContacts = new ObservableCollection<RecipientContact>(backupContacts);
                SelectedPauta.EmailNameFieldId = backupFieldId;
                SyncContacts();
            }

            IsContactMultiSelectMode = false; // Resetear modo al cerrar
        }

        private void LoadPautaData(PautaSchema pauta)
        {
            CurrentContacts = new ObservableCollection<RecipientContact>(pauta.RecipientContacts ?? new());
            CurrentPautaFields = new ObservableCollection<FieldDefinition>(_storageService.LoadConfiguration(pauta.Id).Where(f => f.Type != FieldType.Separator));
        }

        private void AddContact()
        {
            var newContact = new RecipientContact { Name = "Nuevo Nombre", Email = "correo@ejemplo.com" };
            CurrentContacts.Add(newContact);
            SyncContacts();
        }

        private void DeleteContact(RecipientContact? contact)
        {
            if (contact != null)
            {
                if (System.Windows.MessageBox.Show($"¿Desea eliminar a {contact.Name}?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    CurrentContacts.Remove(contact);
                    SyncContacts();
                }
            }
        }

        private void DeleteSelectedContacts()
        {
            var toRemove = CurrentContacts.Where(c => c.IsSelected).ToList();
            if (toRemove.Count == 0) return;

            if (System.Windows.MessageBox.Show($"¿Desea eliminar los {toRemove.Count} contactos seleccionados?", "Confirmar Eliminación Múltiple", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                foreach (var c in toRemove) CurrentContacts.Remove(c);
                SyncContacts();
            }
        }

        private void SyncContacts()
        {
            if (SelectedPauta != null)
            {
                SelectedPauta.RecipientContacts = CurrentContacts.ToList();
            }
        }

        private void ImportContacts()
        {
            if (CurrentContacts.Count > 0)
            {
                var confirm = System.Windows.MessageBox.Show("¡ATENCIÓN! Al importar se ELIMINARÁN todos los contactos actuales y se reemplazarán por los del archivo.\n\n¿Desea realizar un respaldo automático en Excel de sus contactos actuales antes de continuar?", "Importar y Reemplazar", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                if (confirm == MessageBoxResult.Cancel) return;
                if (confirm == MessageBoxResult.Yes)
                {
                    // Auto-respaldo silencioso en la ruta configurada
                    string backupPath = System.IO.Path.Combine(Settings.ExcelExportPath, $"Backup_Contactos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                    ExportContacts(backupPath);
                }
            }

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Importar Directorio (Reemplaza Actual)"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new ClosedXML.Excel.XLWorkbook(openFileDialog.FileName))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var usedRange = worksheet.RangeUsed();
                        if (usedRange == null)
                        {
                            System.Windows.MessageBox.Show("El archivo de Excel parece estar vacío.");
                            return;
                        }

                        var rows = usedRange.RowsUsed().Skip(1);

                        CurrentContacts.Clear(); // LIMPIAR CONTACTOS ACTUALES

                        int count = 0;
                        foreach (var row in rows)
                        {
                            var name = row.Cell(1).GetValue<string>();
                            var email = row.Cell(2).GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                CurrentContacts.Add(new RecipientContact { Name = name, Email = email });
                                count++;
                            }
                        }
                        SyncContacts();
                        System.Windows.MessageBox.Show($"{count} contactos importados y reemplazados correctamente.");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Error al importar: " + ex.Message);
                }
            }
        }

        private void ExportContacts(string? targetPath = null)
        {
            string finalPath = targetPath ?? "";

            if (string.IsNullOrEmpty(finalPath))
            {
                // -- MODO AUTOMÁTICO (Directo a ruta configurada) --
                string exportFolder = Settings.ExcelExportPath;
                string pautaName = SelectedPauta?.Name ?? "Pauta";
                string cleanName = string.Join("_", pautaName.Split(System.IO.Path.GetInvalidFileNameChars()));
                string fileName = $"Contactos_{cleanName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

                if (System.IO.Directory.Exists(exportFolder))
                {
                    // Si la carpeta existe, guardamos directamente sin molestar al usuario
                    finalPath = System.IO.Path.Combine(exportFolder, fileName);
                }
                else
                {
                    // Si la carpeta NO existe, usamos el diálogo como fallback
                    var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "Excel Files (*.xlsx)|*.xlsx",
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        FileName = fileName,
                        Title = "Exportar Contactos a Excel"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        finalPath = saveFileDialog.FileName;
                    }
                    else return;
                }
            }

            try
            {
                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Contactos");
                    worksheet.Cell(1, 1).Value = "Nombre";
                    worksheet.Cell(1, 2).Value = "Correo";

                    int rowNum = 2;
                    // Exportar solo seleccionados si estamos en modo multiselección y hay alguno seleccionado
                    var listToExport = (IsContactMultiSelectMode && CurrentContacts.Any(c => c.IsSelected))
                                       ? CurrentContacts.Where(c => c.IsSelected)
                                       : CurrentContacts;

                    foreach (var contact in listToExport)
                    {
                        worksheet.Cell(rowNum, 1).Value = contact.Name;
                        worksheet.Cell(rowNum, 2).Value = contact.Email;
                        rowNum++;
                    }
                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(finalPath);
                    if (string.IsNullOrEmpty(targetPath)) // Solo avisar si no fue automatización externa
                        System.Windows.MessageBox.Show($"Contactos exportados correctamente en:\n{finalPath}", "Exportación Exitosa");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error al exportar: " + ex.Message);
            }
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
            SyncContacts();

            // 1. Validar Global Templates
            var allFields = Pautas.SelectMany(p => _storageService.LoadConfiguration(p.Id)).ToList();
            var allLabels = new HashSet<string>(allFields.Select(f => f.Label), StringComparer.OrdinalIgnoreCase);
            allLabels.Add("Fecha");

            if (!ValidateTemplateString(Settings.EmailToTemplate, allLabels, "Global (Para)", out string errG1) ||
                !ValidateTemplateString(Settings.EmailCcTemplate, allLabels, "Global (CC)", out errG1) ||
                !ValidateTemplateString(Settings.EmailSubjectTemplate, allLabels, "Global (Asunto)", out errG1) ||
                !ValidateTemplateString(Settings.EmailBodyTemplate, allLabels, "Global (Cuerpo)", out errG1))
            {
                System.Windows.MessageBox.Show(errG1, "Error de Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Validar Pauta Specific Templates
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
                    System.Windows.MessageBox.Show(err, "Error de Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            _storageService.SaveSettings(Settings);
            _storageService.SavePautas(Pautas.ToList());
            System.Windows.MessageBox.Show("Configuración guardada correctamente.", "Éxito");
            IsSaved = true;
            RequestClose?.Invoke();
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
    }
}
