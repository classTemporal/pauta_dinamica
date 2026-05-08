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
        private readonly string _templatesPath;

        public StorageService(string? username = null)
        {
            // Resolve base path based on provided username, current user, or default
            string currentUser = (username ?? SessionService.CurrentUser?.Username ?? "default").Trim();
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PautaDinamica");
            _basePath = Path.Combine(appData, "users", currentUser);

            if (!Directory.Exists(_basePath)) Directory.CreateDirectory(_basePath);
            _pautasIndexPath = Path.Combine(_basePath, "pautas_index.json");
            _lastPautaPath = Path.Combine(_basePath, "last_pauta.txt");
            _settingsPath = Path.Combine(_basePath, "app_settings.json");
            _templatesPath = Path.Combine(_basePath, "templates.json");

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

        public List<MessageTemplate> LoadTemplates()
        {
            if (!File.Exists(_templatesPath)) return new List<MessageTemplate>();
            try
            {
                string json = File.ReadAllText(_templatesPath);
                return JsonSerializer.Deserialize<List<MessageTemplate>>(json) ?? new List<MessageTemplate>();
            }
            catch { return new List<MessageTemplate>(); }
        }

        public void SaveTemplates(List<MessageTemplate> templates)
        {
            try
            {
                string json = JsonSerializer.Serialize(templates, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_templatesPath, json);
            }
            catch { }
        }

        // --- ATTACHMENT MANAGEMENT ---
        public string GetAttachmentsBaseDir()
        {
            string dir = Path.Combine(_basePath, "attachments");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetPautaAttachmentsDir(string pautaId)
        {
            string dir = Path.Combine(GetAttachmentsBaseDir(), pautaId);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public string CopyAttachment(string pautaId, string recordId, string fieldId, string sourcePath)
        {
            if (!File.Exists(sourcePath)) return "";
            
            string destDir = Path.Combine(GetPautaAttachmentsDir(pautaId), recordId, fieldId);
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            
            string fileName = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(destDir, fileName);
            
            // To avoid name collisions, append timestamp if file already exists in this field folder
            if (File.Exists(destPath))
            {
                string nameOnly = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                destPath = Path.Combine(destDir, $"{nameOnly}_{DateTime.Now:yyyyMMddHHmmss}{ext}");
            }
            
            File.Copy(sourcePath, destPath, true);
            return destPath;
        }

        public bool ValidateAttachment(string localPath)
        {
            return !string.IsNullOrEmpty(localPath) && File.Exists(localPath);
        }

        public void DeleteAttachment(string localPath)
        {
            try
            {
                if (File.Exists(localPath)) File.Delete(localPath);
            }
            catch { }
        }

        public void CleanOrphanAttachments(string pautaId, List<AuditEntry> activeRecords)
        {
            // Logic to delete folders for records that no longer exist
            try
            {
                string pautaDir = GetPautaAttachmentsDir(pautaId);
                var recordDirs = Directory.GetDirectories(pautaDir);
                var activeIds = activeRecords.Select(r => r.RecordId).ToHashSet();

                foreach (var dir in recordDirs)
                {
                    string dirName = Path.GetFileName(dir);
                    if (!activeIds.Contains(dirName))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }
            catch { }
        }
    }
}
