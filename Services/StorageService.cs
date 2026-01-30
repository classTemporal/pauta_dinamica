using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Services
{
    public class StorageService
    {
        private readonly string _basePath;
        private readonly string _pautasIndexPath;
        private readonly string _lastPautaPath;
        private readonly string _settingsPath; // Path for global settings

        public StorageService()
        {
            // Resolve base path based on current user
            string currentUser = SessionService.CurrentUser?.Username ?? "default";
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data", "users", currentUser);

            if (!Directory.Exists(_basePath)) Directory.CreateDirectory(_basePath);
            _pautasIndexPath = Path.Combine(_basePath, "pautas_index.json");
            _lastPautaPath = Path.Combine(_basePath, "last_pauta.txt");
            _settingsPath = Path.Combine(_basePath, "app_settings.json");

            EnsureDefaultPautaExists();
        }

        public AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsPath)) return new AppSettings();
            try
            {
                string json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch { return new AppSettings(); }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }

        private void EnsureDefaultPautaExists()
        {
            var pautas = LoadPautas();
            if (!pautas.Any())
            {
                var defaultPauta = new PautaSchema { Name = "Pauta General" };
                SavePautas(new List<PautaSchema> { defaultPauta });
                SetLastPautaId(defaultPauta.Id);

                // Migración: si existe config.json viejo, moverlo a la nueva estructura
                string oldConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                string oldRecords = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit_records.json");

                if (File.Exists(oldConfig))
                {
                    try { File.Move(oldConfig, GetConfigPath(defaultPauta.Id), true); } catch { }
                }
                if (File.Exists(oldRecords))
                {
                    try { File.Move(oldRecords, GetDataPath(defaultPauta.Id), true); } catch { }
                }
            }
        }

        public List<PautaSchema> LoadPautas()
        {
            if (!File.Exists(_pautasIndexPath)) return new List<PautaSchema>();
            try
            {
                string json = File.ReadAllText(_pautasIndexPath);
                var pautas = JsonSerializer.Deserialize<List<PautaSchema>>(json) ?? new List<PautaSchema>();

                // MIGRACIÓN: Si las pautas tienen los antiguos defaults hardcodeados, 
                // los limpiamos para que hereden de la configuración global.
                bool updated = false;
                foreach (var p in pautas)
                {
                    if (p.EmailSubjectTemplate == "Auditoría - [Nombre del Agente]") { p.EmailSubjectTemplate = ""; updated = true; }
                    if (p.EmailBodyTemplate != null && p.EmailBodyTemplate.Contains("Adjunto reporte")) { p.EmailBodyTemplate = ""; updated = true; }
                }

                if (updated) SavePautas(pautas);

                return pautas;
            }
            catch { return new List<PautaSchema>(); }
        }

        public void SavePautas(List<PautaSchema> pautas)
        {
            string json = JsonSerializer.Serialize(pautas, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_pautasIndexPath, json);
        }

        public string GetLastPautaId()
        {
            if (File.Exists(_lastPautaPath)) return File.ReadAllText(_lastPautaPath);
            return LoadPautas().FirstOrDefault()?.Id ?? "";
        }

        public void SetLastPautaId(string id)
        {
            File.WriteAllText(_lastPautaPath, id);
        }

        private string GetConfigPath(string pautaId) => Path.Combine(_basePath, $"pauta_{pautaId}_config.json");
        private string GetDataPath(string pautaId) => Path.Combine(_basePath, $"pauta_{pautaId}_records.json");

        public List<FieldDefinition> LoadConfiguration(string pautaId)
        {
            string path = GetConfigPath(pautaId);
            if (!File.Exists(path))
            {
                var defaultSchema = GetDefaultSchema();
                foreach (var f in defaultSchema) f.EnsureDefaultOptions();
                return defaultSchema;
            }

            try
            {
                string json = File.ReadAllText(path);
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

        public void SaveConfiguration(string pautaId, List<FieldDefinition> config)
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetConfigPath(pautaId), json);
        }

        public void DeletePautaFiles(string pautaId)
        {
            try
            {
                if (File.Exists(GetConfigPath(pautaId))) File.Delete(GetConfigPath(pautaId));
                if (File.Exists(GetDataPath(pautaId))) File.Delete(GetDataPath(pautaId));
            }
            catch { }
        }

        public List<AuditEntry> LoadRecords(string pautaId)
        {
            string path = GetDataPath(pautaId);
            if (!File.Exists(path)) return new List<AuditEntry>();

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<AuditEntry>>(json) ?? new List<AuditEntry>();
            }
            catch { return new List<AuditEntry>(); }
        }

        public void SaveRecords(string pautaId, List<AuditEntry> records)
        {
            string json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetDataPath(pautaId), json);
        }

        public void BackupConfiguration(string pautaId)
        {
            string path = GetConfigPath(pautaId);
            if (!File.Exists(path)) return;

            try
            {
                string backupDir = Path.Combine(_basePath, "backups");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(backupDir, $"config_{pautaId}_{timestamp}.json");

                File.Copy(path, backupPath, true);
            }
            catch { }
        }

        private List<FieldDefinition> GetDefaultSchema()
        {
            return new List<FieldDefinition>
            {
                new FieldDefinition { Id = Guid.NewGuid().ToString(), Label = "Nuevo Campo", Type = FieldType.Text, Order = 1 }
            };
        }
    }
}
