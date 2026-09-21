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
        private string _newTemplateContent = string.Empty;
        private bool _isMultiSelectMode;
        private TemplateItemVM? _editingTemplate;
        private bool _isEditing;
        private string _selectedCategory = string.Empty;

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

        public string SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        public ObservableCollection<string> AvailableCategories { get; } = new();

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

        public event Action? RequestClose;

        public TemplateManagementViewModel(string? pautaId = null)
        {
            _storageService = new StorageService();
            _pautaId = pautaId ?? string.Empty;
            LoadTemplates();

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

        private void MoveUp(TemplateItemVM? item)
        {
            var selected = Templates.Where(t => t.IsSelected).ToList();
            if (!selected.Any()) { if (item != null) selected.Add(item); else return; }

            var orderedSelected = selected.OrderBy(t => Templates.IndexOf(t)).ToList();
            foreach (var t in orderedSelected)
            {
                int idx = Templates.IndexOf(t);
                if (idx > 0 && !Templates[idx - 1].IsSelected)
                {
                    Templates.Move(idx, idx - 1);
                }
            }
        }

        private void MoveDown(TemplateItemVM? item)
        {
            var selected = Templates.Where(t => t.IsSelected).ToList();
            if (!selected.Any()) { if (item != null) selected.Add(item); else return; }

            var orderedSelected = selected.OrderByDescending(t => Templates.IndexOf(t)).ToList();
            foreach (var t in orderedSelected)
            {
                int idx = Templates.IndexOf(t);
                if (idx < Templates.Count - 1 && !Templates[idx + 1].IsSelected)
                {
                    Templates.Move(idx, idx + 1);
                }
            }
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
            Templates = new ObservableCollection<TemplateItemVM>(models.Select(m => new TemplateItemVM(m)));

            // Build available categories from existing templates
            var cats = models.Where(t => !string.IsNullOrWhiteSpace(t.Category))
                .Select(t => t.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();
            AvailableCategories.Clear();
            foreach (var c in cats) AvailableCategories.Add(c);
        }

        private void SaveTemplates()
        {
            // Templates are stored globally with PautaId association.
            // Load the global store, remove all templates for this pauta, then add the current ones.
            var global = _storageService.LoadTemplates();
            global.RemoveAll(t => t.PautaId == _pautaId);
            var updated = Templates.Select(t =>
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
                if (!string.IsNullOrWhiteSpace(_selectedCategory))
                    _editingTemplate.Model.Category = _selectedCategory;
                else
                    _editingTemplate.Model.Category = string.Empty;
                _editingTemplate.NotifyContentChanged();
                CancelEdit();
            }
            else
            {
                var newModel = new MessageTemplate { Content = NewTemplateContent.Trim(), PautaId = _pautaId, Category = _selectedCategory };
                Templates.Add(new TemplateItemVM(newModel));
                NewTemplateContent = string.Empty;
            }
        }

        private void StartEdit(TemplateItemVM? template)
        {
            if (template == null) return;
            _editingTemplate = template;
            NewTemplateContent = template.Model.Content;
            SelectedCategory = template.Model.Category ?? string.Empty;
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
                    Templates.Remove(template);
                }
            }
        }

        private void DeleteSelected()
        {
            var toRemove = Templates.Where(t => t.IsSelected).ToList();
            if (!toRemove.Any()) return;

            if (MessageBoxHelper.ShowNonCritical($"¿Eliminar {toRemove.Count} plantillas seleccionadas?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                foreach (var t in toRemove) Templates.Remove(t);
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
                                Templates.Add(new TemplateItemVM(new MessageTemplate { Content = content, PautaId = _pautaId, Category = category }));
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
        }
    }
}
