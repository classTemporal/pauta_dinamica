using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PautaDinamicaApp;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace PautaDinamicaApp.ViewModels
{
    public class TemplateItemVM : ViewModelBase
    {
        private bool _isSelected;
        public MessageTemplate Model { get; }
        public string Content => Model.Content;
        public string Category => Model.Category ?? string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public TemplateItemVM(MessageTemplate model)
        {
            Model = model;
        }

        public void NotifyContentChanged()
        {
            OnPropertyChanged(nameof(Content));
        }
    }

    public class TemplateManagementViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;
        private readonly string _pautaId;
        private ObservableCollection<TemplateItemVM> _templates = new();
        private ObservableCollection<TemplateItemVM> _allTemplates = new();
        private string _newTemplateContent = string.Empty;
        private bool _isMultiSelectMode;
        private TemplateItemVM? _editingTemplate;
        private bool _isEditing;

        public ObservableCollection<TemplateItemVM> Templates { get => _templates; set => SetProperty(ref _templates, value); }
        public string NewTemplateContent { get => _newTemplateContent; set => SetProperty(ref _newTemplateContent, value); }
        public bool IsMultiSelectMode { get => _isMultiSelectMode; set => SetProperty(ref _isMultiSelectMode, value); }
        
        public bool IsEditing 
        { 
            get => _isEditing; 
            set 
            { 
                if (SetProperty(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(AddButtonText));
                }
            } 
        }

        public string AddButtonText => IsEditing ? "💾 ACTUALIZAR" : "➕ AGREGAR";

        private string _selectedCategory = "No categorizado";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    ApplyCategoryFilter();
                }
            }
        }

        /// <summary>Selection in the categories ListBox — does NOT drive the template filter, just the UI. Kept separate to avoid StackOverflow loops.</summary>
        private string? _categoryListSelection;
        public string? CategoryListSelection
        {
            get => _categoryListSelection;
            set => SetProperty(ref _categoryListSelection, value);
        }

        private bool _isCategorizedMode;
        public bool IsCategorizedMode
        {
            get => _isCategorizedMode;
            set => SetProperty(ref _isCategorizedMode, value);
        }

        public ObservableCollection<string> AvailableCategories { get; } = new();
        /// <summary>Categories for the template-creation dropdown, includes "No categorizado" as default.</summary>
        public ObservableCollection<string> CategoryOptions { get; } = new();
        public ICommand AddTemplateCommand { get; }
        public ICommand DeleteTemplateCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ToggleMultiSelectCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ImportExcelCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand StartEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand ApplyChangesCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand CreateCategoryCommand { get; }
        public ICommand DeleteCategoryCommand { get; }
        public ICommand DeleteCategoryAtCommand { get; }

        public event Action? RequestClose;

        public string NewCategoryText { get; set; } = string.Empty;

        public TemplateManagementViewModel(string? pautaId = null)
        {
            _storageService = new StorageService();
            _pautaId = pautaId ?? string.Empty;
            LoadTemplates();

            CreateCategoryCommand = new RelayCommand(_ => CreateCategory());
            DeleteCategoryCommand = new RelayCommand(_ => DeleteCategory());
            DeleteCategoryAtCommand = new RelayCommand(c => DeleteCategoryAt(c as string));

            AddTemplateCommand = new RelayCommand(_ => AddTemplate(), _ => !string.IsNullOrWhiteSpace(NewTemplateContent));
            DeleteTemplateCommand = new RelayCommand(p => DeleteTemplate(p as TemplateItemVM));
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected());
            ToggleMultiSelectCommand = new RelayCommand(_ => { IsMultiSelectMode = !IsMultiSelectMode; if (!IsMultiSelectMode) SelectNone(); });
            ExportExcelCommand = new RelayCommand(_ => ExportToExcel());
            ImportExcelCommand = new RelayCommand(_ => ImportFromExcel());
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            MoveUpCommand = new RelayCommand(p => MoveUp(p as TemplateItemVM));
            MoveDownCommand = new RelayCommand(p => MoveDown(p as TemplateItemVM));
            StartEditCommand = new RelayCommand(p => StartEdit(p as TemplateItemVM));
            CancelEditCommand = new RelayCommand(_ => CancelEdit());
            SaveChangesCommand = new RelayCommand(_ => SaveAndClose());
            ApplyChangesCommand = new RelayCommand(_ => { SaveTemplates(); MessageBoxHelper.ShowNonCritical("Plantillas aplicadas correctamente.", "Éxito"); });
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }

        private void ApplyCategoryFilter()
        {
            // Filter Templates based on selected category (does NOT rebuild CategoryOptions to avoid ComboBox StackOverflow)
            if (_selectedCategory == "No categorizado")
            {
                Templates = new ObservableCollection<TemplateItemVM>(_allTemplates);
            }
            else
            {
                Templates = new ObservableCollection<TemplateItemVM>(_allTemplates.Where(t => string.Equals(t.Model.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase)));
            }
        }

        /// <summary>Rebuilds CategoryOptions (dropdown) + AvailableCategories (categories tab) + restores valid selection.</summary>
        private void RefreshCategoryOptions()
        {
            // Build available categories excluding "No categorizado" (internal default, not user-editable)
            AvailableCategories.Clear();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var persistedCategories = _storageService.LoadTemplateCategories().OrderBy(c => c, StringComparer.OrdinalIgnoreCase);
            foreach (var c in persistedCategories)
                if (c != "No categorizado" && seen.Add(c))
                    AvailableCategories.Add(c);
            var templateCategories = _allTemplates
                .Where(t => !string.IsNullOrWhiteSpace(t.Model.Category) && t.Model.Category != "No categorizado")
                .Select(t => t.Model.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var c in templateCategories)
                if (seen.Add(c))
                    AvailableCategories.Add(c);

            // Restore selection if still valid; otherwise default to "No categorizado" (shows all)
            if (string.IsNullOrWhiteSpace(_selectedCategory) || (AvailableCategories.Count > 0 && !AvailableCategories.Any(c => c.Equals(_selectedCategory, StringComparison.OrdinalIgnoreCase))))
                _selectedCategory = "No categorizado";

            // Populate dropdown options with "No categorizado" + real categories
            CategoryOptions.Clear();
            CategoryOptions.Add("No categorizado");
            foreach (var c in AvailableCategories)
                CategoryOptions.Add(c);
        }

        private void MoveUp(TemplateItemVM? item)
        {
            var selected = _allTemplates.Where(t => t.IsSelected).ToList();
            if (!selected.Any()) { if (item != null) selected.Add(item); else return; }

            var orderedSelected = selected.OrderBy(t => _allTemplates.IndexOf(t)).ToList();
            foreach (var t in orderedSelected)
            {
                int idx = _allTemplates.IndexOf(t);
                if (idx > 0 && !_allTemplates[idx - 1].IsSelected)
                {
                    _allTemplates.Move(idx, idx - 1);
                }
            }
            ApplyCategoryFilter();
        }

        private void MoveDown(TemplateItemVM? item)
        {
            var selected = _allTemplates.Where(t => t.IsSelected).ToList();
            if (!selected.Any()) { if (item != null) selected.Add(item); else return; }

            var orderedSelected = selected.OrderByDescending(t => _allTemplates.IndexOf(t)).ToList();
            foreach (var t in orderedSelected)
            {
                int idx = _allTemplates.IndexOf(t);
                if (idx < _allTemplates.Count - 1 && !_allTemplates[idx + 1].IsSelected)
                {
                    _allTemplates.Move(idx, idx + 1);
                }
            }
            ApplyCategoryFilter();
        }

        private void SaveAndClose()
        {
            SaveTemplates();
            MessageBoxHelper.ShowNonCritical("Plantillas guardadas correctamente.", "Éxito");
            RequestClose?.Invoke();
        }

        private void LoadTemplates()
        {
            var models = _storageService.LoadTemplatesForPauta(_pautaId);
            _allTemplates = new ObservableCollection<TemplateItemVM>(models.Select(m => new TemplateItemVM(m)));

            IsCategorizedMode = models.Any(t => !string.IsNullOrWhiteSpace(t.Category)) || _storageService.LoadTemplateCategories().Any();

            // RefreshCategoryOptions rebuilds AvailableCategories + CategoryOptions + filters Templates
            RefreshCategoryOptions();
        }

        private void SaveTemplates()
        {
            // Templates are stored globally with PautaId association.
            // Load the global store, remove all templates for this pauta, then add the current ones.
            var global = _storageService.LoadTemplates();
            global.RemoveAll(t => t.PautaId == _pautaId);
            var updated = _allTemplates.Select(t =>
            {
                t.Model.PautaId = _pautaId;
                return t.Model;
            }).ToList();
            global.AddRange(updated);
            _storageService.SaveTemplates(global);
        }

        private void AddTemplate()
        {
            if (IsEditing && _editingTemplate != null)
            {
                _editingTemplate.Model.Content = NewTemplateContent.Trim();
                _editingTemplate.Model.PautaId = _pautaId;
                _editingTemplate.Model.Category = string.IsNullOrWhiteSpace(_selectedCategory) ? "No categorizado" : _selectedCategory;
                _editingTemplate.NotifyContentChanged();
                CancelEdit();
            }
            else
            {
                var newModel = new MessageTemplate
                {
                    Content = NewTemplateContent.Trim(),
                    PautaId = _pautaId,
                    Category = string.IsNullOrWhiteSpace(_selectedCategory) ? "No categorizado" : _selectedCategory
                };
                _allTemplates.Add(new TemplateItemVM(newModel));
                NewTemplateContent = string.Empty;
            }
            RefreshCategoryOptions();
        }

        private void StartEdit(TemplateItemVM? template)
        {
            if (template == null) return;
            _editingTemplate = template;
            NewTemplateContent = template.Model.Content;
            SelectedCategory = string.IsNullOrWhiteSpace(template.Model.Category) ? "No categorizado" : template.Model.Category;
            IsEditing = true;
        }

        private void CancelEdit()
        {
            _editingTemplate = null;
            NewTemplateContent = string.Empty;
            IsEditing = false;
        }

        private void DeleteTemplate(TemplateItemVM? template)
        {
            if (template != null)
            {
                if (MessageBoxHelper.ShowNonCritical("¿Eliminar esta plantilla?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    if (_editingTemplate == template) CancelEdit();
                    _allTemplates.Remove(template);
                    ApplyCategoryFilter();
                }
            }
        }

        private void DeleteSelected()
        {
            var toRemove = Templates.Where(t => t.IsSelected).ToList();
            if (!toRemove.Any()) return;

            if (MessageBoxHelper.ShowNonCritical($"¿Eliminar {toRemove.Count} plantillas seleccionadas?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                foreach (var t in toRemove) _allTemplates.Remove(t);
                ApplyCategoryFilter();
            }
        }

        private void SelectAll()
        {
            bool anyUnselected = Templates.Any(t => !t.IsSelected);
            foreach (var t in Templates) t.IsSelected = anyUnselected;
        }

        private void SelectNone()
        {
            foreach (var t in Templates) t.IsSelected = false;
        }

        private void ExportToExcel()
        {
            var listToExport = IsMultiSelectMode && Templates.Any(t => t.IsSelected)
                ? Templates.Where(t => t.IsSelected).ToList()
                : Templates.ToList();

            if (!listToExport.Any()) return;

            var settings = _storageService.LoadSettings();
            string exportFolder = settings.ExcelExportPath;
            string fileName = $"Plantillas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string finalPath = "";

            if (System.IO.Directory.Exists(exportFolder))
            {
                finalPath = Path.Combine(exportFolder, fileName);
            }
            else
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
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
                    var worksheet = workbook.Worksheets.Add("Plantillas");
                    worksheet.Cell(1, 1).Value = "Contenido";
                    worksheet.Cell(1, 1).Style.Font.Bold = true;
                    worksheet.Cell(1, 2).Value = "Categoría";
                    worksheet.Cell(1, 2).Style.Font.Bold = true;

                    int rowNum = 2;
                    foreach (var t in listToExport)
                    {
                        worksheet.Cell(rowNum, 1).Value = t.Model.Content;
                        if (t.Model.Content.Contains("\n")) worksheet.Cell(rowNum, 1).Style.Alignment.SetWrapText(true);
                        worksheet.Cell(rowNum, 2).Value = t.Model.Category ?? "";
                        rowNum++;
                    }
                    worksheet.Column(1).Width = 100;
                    worksheet.Column(2).Width = 30;
                    workbook.SaveAs(finalPath);
                    MessageBoxHelper.ShowNonCritical($"Plantillas exportadas exitosamente en:\n{finalPath}", "Éxito");
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportFromExcel()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx" };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new XLWorkbook(ofd.FileName))
                    {
                        var worksheet = workbook.Worksheets.FirstOrDefault();
                        if (worksheet == null) return;

                        var rows = worksheet.RowsUsed().Skip(1);
                        int count = 0;
                        foreach (var row in rows)
                        {
                            string content = row.Cell(1).GetValue<string>();
                            string category = row.Cell(2).GetValue<string>() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                _allTemplates.Add(new TemplateItemVM(new MessageTemplate { Content = content, PautaId = _pautaId, Category = category }));
                                count++;
                            }
                        }
                        if (count > 0)
                        {
                            MessageBoxHelper.ShowNonCritical($"{count} plantillas importadas. Recuerde guardar los cambios.", "Éxito");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBoxHelper.Show("Error al importar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            RefreshCategoryOptions();
        }

        /// <summary>
        /// Crea una nueva categoría persistiéndola en template_categories.json.
        /// </summary>
        private void CreateCategory()
        {
            string name = NewCategoryText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBoxHelper.Show("El nombre de la categoría no puede estar vacío.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check for duplicate in file + AvailableCategories
            var fileCategories = _storageService.LoadTemplateCategories();
            if (fileCategories.Any(c => c.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBoxHelper.Show("La categoría \"" + name + "\" ya existe.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (AvailableCategories.Any(c => c.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBoxHelper.Show("La categoría \"" + name + "\" ya existe.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Persist to template_categories.json
            fileCategories.Add(name);
            _storageService.SaveTemplateCategories(fileCategories);

            // Preserve SelectedCategory if still valid; otherwise default
            string previousSelection = _selectedCategory;
            LoadTemplates();
            if (!string.Equals(_selectedCategory, previousSelection, StringComparison.OrdinalIgnoreCase) || _selectedCategory == "No categorizado")
            {
                // Ensure new category is visible if it was just created
                if (AvailableCategories.Contains(name, StringComparer.OrdinalIgnoreCase))
                    SelectedCategory = name;
            }

            NewCategoryText = string.Empty;
            OnPropertyChanged(nameof(NewCategoryText));
        }

        /// <summary>
        /// Elimina una categoría específica por nombre (viene del botón inline de la lista).
        /// </summary>
        private void DeleteCategoryAt(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName) || categoryName == "No categorizado")
            {
                MessageBoxHelper.Show("No se puede eliminar 'No categorizado'.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBoxHelper.ShowNonCritical("¿Eliminar la categoría \"" + categoryName + "\" y todas sus plantillas?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // Remove from categories file
            var categories = _storageService.LoadTemplateCategories();
            categories.RemoveAll(c => c.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            _storageService.SaveTemplateCategories(categories);

            // Remove templates with this category
            var toRemove = _allTemplates.Where(t => string.Equals(t.Model.Category, categoryName, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var t in toRemove) _allTemplates.Remove(t);

            SaveTemplates();
            LoadTemplates();
        }

        /// <summary>
        /// Elimina una categoría: la borra de template_categories.json y borra sus templates.
        /// </summary>
        private void DeleteCategory()
        {
            string catName = _categoryListSelection ?? string.Empty;
            if (string.IsNullOrWhiteSpace(catName))
            {
                MessageBoxHelper.Show("Selecciona una categoría para eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (catName == "No categorizado")
            {
                MessageBoxHelper.Show("No se puede eliminar la categoría 'No categorizado'.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBoxHelper.ShowNonCritical("¿Eliminar la categoría \"" + catName + "\" y todas sus plantillas?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // Remove from categories file
            var categories = _storageService.LoadTemplateCategories();
            categories.RemoveAll(c => c.Equals(catName, StringComparison.OrdinalIgnoreCase));
            _storageService.SaveTemplateCategories(categories);

            // Remove templates with this category
            var toRemove = _allTemplates.Where(t => string.Equals(t.Model.Category, catName, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var t in toRemove) _allTemplates.Remove(t);

            SaveTemplates();
            LoadTemplates();
        }
    }
}