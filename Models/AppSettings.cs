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

        // Email Settings
        public EmailMethod SelectedEmailMethod { get; set; } = EmailMethod.Mailto;
        public string EmailToTemplate { get; set; } = "";
        public string EmailCcTemplate { get; set; } = "";
        public string EmailSubjectTemplate { get; set; } = "";
        public string EmailBodyTemplate { get; set; } = "";


        // UI Theme
        public Services.AppTheme Theme { get; set; } = Services.ThemeService.GetSystemTheme();

        // Admin Settings
        public bool EnableInternalTimer { get; set; } = true;

        // Spellcheck Settings
        public string SpellCheckLanguage { get; set; } = "es-ES"; // Default to Spanish
    }
}

