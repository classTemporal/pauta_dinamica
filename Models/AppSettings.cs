using System;

namespace PautaDinamicaApp.Models
{
    public enum EmailMethod
    {
        Mailto,
        Outlook
    }

    public enum DashboardLayout
    {
        Left,
        Right,
        Top,
        Bottom,
        BottomNoSticky
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

        // UI Persistence
        public double DashboardFormWidth { get; set; } = 400;
        public double DashboardFormHeight { get; set; } = 300;
        public bool IsFiltersPanelExpanded { get; set; } = true;
        public DashboardLayout DashboardLayout { get; set; } = DashboardLayout.Left;
    }
}
