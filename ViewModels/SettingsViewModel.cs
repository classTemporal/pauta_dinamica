using System;
using System.Windows.Input;
using System.Windows.Forms; // Using WinForms for FolderBrowserDialog
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;

namespace PautaDinamicaApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly StorageService _storageService;
        private AppSettings _settings;

        public AppSettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        public ICommand BrowseExcelPathCommand { get; }
        public ICommand BrowseJsonPathCommand { get; }
        public ICommand BrowsePdfPathCommand { get; }
        public ICommand SaveCommand { get; }

        public event Action? RequestClose;

        public SettingsViewModel()
        {
            _storageService = new StorageService();
            _settings = _storageService.LoadSettings();

            BrowseExcelPathCommand = new RelayCommand(_ => BrowseFolder(path => Settings.ExcelExportPath = path));
            BrowseJsonPathCommand = new RelayCommand(_ => BrowseFolder(path => Settings.JsonBackupPath = path));
            BrowsePdfPathCommand = new RelayCommand(_ => BrowseFolder(path => Settings.PdfReportPath = path));
            SaveCommand = new RelayCommand(_ => SaveAndClose());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }

        public ICommand CancelCommand { get; }

        private void BrowseFolder(Action<string> updateAction)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    updateAction(dialog.SelectedPath);
                    OnPropertyChanged(nameof(Settings)); // Refresh bindings
                }
            }
        }

        private void SaveAndClose()
        {
            _storageService.SaveSettings(Settings);
            System.Windows.MessageBox.Show("Configuración guardada correctamente.", "Éxito");
            RequestClose?.Invoke();
        }
    }
}
