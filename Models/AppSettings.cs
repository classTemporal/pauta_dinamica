using System;

namespace PautaDinamicaApp.Models
{
    public class AppSettings
    {
        public string ExcelExportPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string JsonBackupPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string PdfReportPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}
