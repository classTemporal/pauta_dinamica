using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public TemplateItemVM(MessageTemplate model)
        {
            Model = model;
        }
    }

    public class TemplateManagementViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;
        private ObservableCollection<TemplateItemVM> _templates = new();
        private string _newTemplateContent = string.Empty;
        private bool _isMultiSelectMode;

        public ObservableCollection<TemplateItemVM> Templates { get => _templates; set => SetProperty(ref _templates, value); }
        public string NewTemplateContent { get => _newTemplateContent; set => SetProperty(ref _newTemplateContent, value); }
        public bool IsMultiSelectMode { get => _isMultiSelectMode; set => SetProperty(ref _isMultiSelectMode, value); }

        public ICommand AddTemplateCommand { get; }
        public ICommand DeleteTemplateCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ToggleMultiSelectCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ImportExcelCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action? RequestClose;

        public TemplateManagementViewModel()
        {
            _storageService = new StorageService();
            LoadTemplates();

            AddTemplateCommand = new RelayCommand(_ => AddTemplate(), _ => !string.IsNullOrWhiteSpace(NewTemplateContent));
            DeleteTemplateCommand = new RelayCommand(p => DeleteTemplate(p as TemplateItemVM));
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected());
            ToggleMultiSelectCommand = new RelayCommand(_ => { IsMultiSelectMode = !IsMultiSelectMode; if (!IsMultiSelectMode) SelectNone(); });
            ExportExcelCommand = new RelayCommand(_ => ExportToExcel());
            ImportExcelCommand = new RelayCommand(_ => ImportFromExcel());
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            SaveChangesCommand = new RelayCommand(_ => SaveAndClose());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }

        private void SaveAndClose()
        {
            SaveTemplates();
            System.Windows.MessageBox.Show("Plantillas guardadas correctamente.", "Éxito");
            RequestClose?.Invoke();
        }

        private void LoadTemplates()
        {
            var models = _storageService.LoadTemplates();
            Templates = new ObservableCollection<TemplateItemVM>(models.Select(m => new TemplateItemVM(m)));
        }

        private void SaveTemplates()
        {
            _storageService.SaveTemplates(Templates.Select(t => t.Model).ToList());
        }

        private void AddTemplate()
        {
            var newModel = new MessageTemplate { Content = NewTemplateContent.Trim() };
            Templates.Add(new TemplateItemVM(newModel));
            NewTemplateContent = string.Empty;
        }

        private void DeleteTemplate(TemplateItemVM? template)
        {
            if (template != null)
            {
                if (System.Windows.MessageBox.Show("¿Eliminar esta plantilla?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Templates.Remove(template);
                }
            }
        }

        private void DeleteSelected()
        {
            var toRemove = Templates.Where(t => t.IsSelected).ToList();
            if (!toRemove.Any()) return;

            if (System.Windows.MessageBox.Show($"¿Eliminar {toRemove.Count} plantillas seleccionadas?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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
            string fileName = $"Plantillas_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
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
                    worksheet.Cell(1, 1).Style.Font.Bold = true; // Keep this line from original

                    int rowNum = 2;
                    foreach (var t in listToExport)
                    {
                        worksheet.Cell(rowNum, 1).Value = t.Model.Content;
                        if (t.Model.Content.Contains("\n")) worksheet.Cell(rowNum, 1).Style.Alignment.SetWrapText(true);
                        rowNum++;
                    }
                    worksheet.Column(1).Width = 100;
                    workbook.SaveAs(finalPath);
                    System.Windows.MessageBox.Show($"Plantillas exportadas exitosamente en:\n{finalPath}", "Éxito");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al exportar: {ex.Message}");
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
                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                Templates.Add(new TemplateItemVM(new MessageTemplate { Content = content }));
                                count++;
                            }
                        }
                        if (count > 0)
                        {
                            System.Windows.MessageBox.Show($"{count} plantillas importadas. Recuerde guardar los cambios.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Error al importar: " + ex.Message);
                }
            }
        }
    }
}
