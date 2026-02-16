using System;

namespace PautaDinamicaApp.Models
{
    public enum EmailMethod
    {
        Mailto,
        Outlook
    }

    public class AppSettings
    {
        public string ExcelExportPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string JsonBackupPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string PdfReportPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // UI Theme
        public Services.AppTheme Theme { get; set; } = Services.ThemeService.GetSystemTheme();

        // Admin Settings
        public bool EnableInternalTimer { get; set; } = true;

        // Spellcheck Settings
        public string SpellCheckLanguage { get; set; } = "es-ES"; // Default to Spanish
    }
}

