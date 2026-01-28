using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace PautaDinamicaApp.ViewModels
{
    public class SelectableOptionVM : ViewModelBase
    {
        private string _text = "";
        private bool _isSelected;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public SelectableOptionVM(string text) { Text = text; }
    }

    public class OptionsEditorViewModel : ViewModelBase
    {
        private ObservableCollection<SelectableOptionVM> _options;
        private string _newOptionText = string.Empty;
        private bool _isMultiSelectMode;

        public OptionsEditorViewModel(System.Collections.Generic.List<string> existingOptions)
        {
            var wrapped = (existingOptions ?? new List<string>()).Select(s => new SelectableOptionVM(s));
            _options = new ObservableCollection<SelectableOptionVM>(wrapped);

            AddOptionCommand = new RelayCommand(_ => AddOption(), _ => !string.IsNullOrWhiteSpace(NewOptionText));
            RemoveOptionCommand = new RelayCommand(p => RemoveOption(p as SelectableOptionVM));

            ToggleMultiSelectCommand = new RelayCommand(_ => ToggleMultiSelect());
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected());
            ExportOptionsCommand = new RelayCommand(_ => ExportToExcel(Options, "Opciones_Todas"));
            ExportSelectedOptionsCommand = new RelayCommand(_ => ExportToExcel(Options.Where(o => o.IsSelected), "Opciones_Seleccionadas"));
        }

        public ObservableCollection<SelectableOptionVM> Options
        {
            get => _options;
            set => SetProperty(ref _options, value);
        }

        // Return pure list of strings for the parent to save
        public List<string> ResultOptions => Options.Select(o => o.Text).ToList();

        public string NewOptionText
        {
            get => _newOptionText;
            set => SetProperty(ref _newOptionText, value);
        }

        public bool IsMultiSelectMode
        {
            get => _isMultiSelectMode;
            set => SetProperty(ref _isMultiSelectMode, value);
        }

        public ICommand AddOptionCommand { get; }
        public ICommand RemoveOptionCommand { get; }
        public ICommand ToggleMultiSelectCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ExportOptionsCommand { get; }
        public ICommand ExportSelectedOptionsCommand { get; }

        private void ExportToExcel(IEnumerable<SelectableOptionVM> list, string fileNameBase)
        {
            var data = list.ToList();
            if (!data.Any())
            {
                MessageBox.Show("No hay opciones para exportar.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"{fileNameBase}_{DateTime.Now:yyyyMMdd_HHmm}"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Opciones");

                        // Header
                        var headerCell = worksheet.Cell(1, 1);
                        headerCell.Value = "Opción";
                        headerCell.Style.Font.Bold = true;
                        headerCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#007bff");
                        headerCell.Style.Font.FontColor = XLColor.White;

                        // Data
                        int row = 2;
                        foreach (var item in data)
                        {
                            worksheet.Cell(row++, 1).Value = item.Text;
                        }

                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(sfd.FileName);
                        MessageBox.Show("Opciones exportadas correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddOption()
        {
            if (string.IsNullOrWhiteSpace(NewOptionText)) return;

            string cleaned = NewOptionText.Trim();
            if (Options.Any(o => o.Text.Equals(cleaned, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Esta opción ya existe en la lista.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Options.Add(new SelectableOptionVM(cleaned));
            NewOptionText = string.Empty;
        }

        private void RemoveOption(SelectableOptionVM? option)
        {
            if (option == null) return;

            var result = MessageBox.Show($"¿Realmente desea eliminar la opción \"{option.Text}\"?",
                                       "Confirmar eliminación",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Options.Remove(option);
            }
        }

        private void ToggleMultiSelect()
        {
            IsMultiSelectMode = !IsMultiSelectMode;
            if (!IsMultiSelectMode)
            {
                foreach (var o in Options) o.IsSelected = false;
            }
        }

        private void SelectAll()
        {
            bool allSelected = Options.All(o => o.IsSelected);
            foreach (var o in Options) o.IsSelected = !allSelected;
        }

        private void DeleteSelected()
        {
            var selected = Options.Where(o => o.IsSelected).ToList();
            if (!selected.Any()) return;

            var result = MessageBox.Show($"¿Eliminar {selected.Count} opciones seleccionadas?",
                                       "Confirmar eliminación múltiple",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var s in selected) Options.Remove(s);
            }
        }
    }
}
