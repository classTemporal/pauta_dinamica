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

        // Row Coloring Rule
        public string ColoringField { get; set; } = "";
        public string ColoringValue { get; set; } = "";
        public string ColoringColor { get; set; } = "#28a745"; // Default Green

        // UI Theme
        public Services.AppTheme Theme { get; set; } = Services.AppTheme.Dark;
    }
}

