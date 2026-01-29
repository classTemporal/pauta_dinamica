using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Services
{
    public class StorageService
    {
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private readonly string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit_records.json");

        public List<FieldDefinition> LoadConfiguration()
        {
            if (!File.Exists(_configPath))
            {
                var defaultSchema = GetDefaultSchema();
                foreach (var f in defaultSchema) f.EnsureDefaultOptions();
                SaveConfiguration(defaultSchema);
                return defaultSchema;
            }

            try
            {
                string json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<List<FieldDefinition>>(json) ?? GetDefaultSchema();
                foreach (var f in config) f.EnsureDefaultOptions();
                return config;
            }
            catch
            {
                var defaultSchema = GetDefaultSchema();
                foreach (var f in defaultSchema) f.EnsureDefaultOptions();
                return defaultSchema;
            }
        }

        public void SaveConfiguration(List<FieldDefinition> config)
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }

        public void BackupConfiguration()
        {
            if (!File.Exists(_configPath)) return;

            try
            {
                string backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(backupDir, $"config_backup_{timestamp}.json");

                File.Copy(_configPath, backupPath, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating backup: {ex.Message}");
            }
        }

        public void BackupRecords()
        {
            if (!File.Exists(_dataPath)) return;

            try
            {
                string backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(backupDir, $"records_backup_{timestamp}.json");

                File.Copy(_dataPath, backupPath, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating record backup: {ex.Message}");
            }
        }

        public List<AuditEntry> LoadRecords()
        {
            if (!File.Exists(_dataPath)) return new List<AuditEntry>();

            try
            {
                string json = File.ReadAllText(_dataPath);
                return JsonSerializer.Deserialize<List<AuditEntry>>(json) ?? new List<AuditEntry>();
            }
            catch
            {
                return new List<AuditEntry>();
            }
        }

        public void SaveRecords(List<AuditEntry> records)
        {
            string json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataPath, json);
        }

        private List<FieldDefinition> GetDefaultSchema()
        {
            return new List<FieldDefinition>
            {
                // Datos del Ticket
                new FieldDefinition { Id = "f_01", Label = "Ticket", Category = "Datos del Ticket", Type = FieldType.Text, IsRequired = false, Order = 1 },
                new FieldDefinition { Id = "f_02", Label = "Servicio", Category = "Datos del Ticket", Type = FieldType.Dropdown, Options = new List<string> { "Mexico", "USA", "Other" }, IsRequired = false, Order = 2 },
                new FieldDefinition { Id = "f_03", Label = "Analista", Category = "Datos del Ticket", Type = FieldType.Dropdown, Options = new List<string> { "Analista 1", "Analista 2" }, IsRequired = false, Order = 3 },
                
                // Evaluación de llamada (PENC)
                new FieldDefinition { Id = "p_01", Label = "Bienvenida e identificación", Category = "Evaluación de llamada (PENC)", Type = FieldType.Dropdown, Options = new List<string> { "1", "0", "N/A" }, IsRequired = false, Order = 4 },
                new FieldDefinition { Id = "p_02", Label = "Indagar", Category = "Evaluación de llamada (PENC)", Type = FieldType.Dropdown, Options = new List<string> { "1", "0", "N/A" }, IsRequired = false, Order = 5 },
                new FieldDefinition { Id = "p_03", Label = "Personalización", Category = "Evaluación de llamada (PENC)", Type = FieldType.Dropdown, Options = new List<string> { "1", "0", "N/A" }, IsRequired = false, Order = 6 },
                
                // Evaluación PEC
                new FieldDefinition { Id = "pec_01", Label = "No corta llamada", Category = "Evaluación PEC (Alto riesgo)", Type = FieldType.Dropdown, Options = new List<string> { "1", "0", "N/A" }, IsRequired = false, Order = 7 },
                new FieldDefinition { Id = "pec_02", Label = "Cumple con políticas", Category = "Evaluación PEC (Alto riesgo)", Type = FieldType.Dropdown, Options = new List<string> { "1", "0", "N/A" }, IsRequired = false, Order = 8 }
            };
        }
    }
}
