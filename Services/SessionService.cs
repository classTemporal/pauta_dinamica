using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Services
{
    public class SessionService
    {
        private readonly string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PautaDinamica");
        private string _usersPath => Path.Combine(_appDataPath, "users_index.json");
        private static UserModel? _currentUser;

        public static UserModel? CurrentUser => _currentUser;

        public SessionService()
        {
            if (!Directory.Exists(_appDataPath)) Directory.CreateDirectory(_appDataPath);
        }

        public List<UserModel> LoadUsers()
        {
            if (!File.Exists(_usersPath)) return new List<UserModel>();
            try
            {
                string json = File.ReadAllText(_usersPath);
                var users = JsonSerializer.Deserialize<List<UserModel>>(json) ?? new List<UserModel>();

                // Limpiar espacios de usuarios existentes (migración silenciosa)
                bool changed = false;
                foreach (var u in users)
                {
                    if (u.Username != u.Username.Trim())
                    {
                        u.Username = u.Username.Trim();
                        changed = true;
                    }
                }

                if (changed)
                {
                    // Eliminar duplicados que puedan haber quedado tras el Trim
                    var uniqueUsers = users.GroupBy(u => u.Username.ToLower()).Select(g => g.First()).ToList();
                    SaveUsers(uniqueUsers);
                    return uniqueUsers;
                }

                return users;
            }
            catch { return new List<UserModel>(); }
        }

        public void SaveUsers(List<UserModel> users)
        {
            string json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_usersPath, json);
        }

        public void RegisterUser(string username, string? password)
        {
            username = username?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(username)) throw new Exception("El nombre de usuario no puede estar vacío.");
            if (username.Contains(" ")) throw new Exception("El nombre de usuario no puede contener espacios.");

            var users = LoadUsers();
            if (users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                throw new Exception("El usuario ya existe.");

            var newUser = new UserModel
            {
                Username = username,
                PasswordHash = string.IsNullOrWhiteSpace(password) ? null : HashPassword(password)
            };
            users.Add(newUser);
            SaveUsers(users);

            // Create user data folder
            string userDir = Path.Combine(_appDataPath, "users", username);
            if (!Directory.Exists(userDir)) Directory.CreateDirectory(userDir);
        }

        public bool Login(string username, string? password)
        {
            username = username?.Trim() ?? "";
            var users = LoadUsers();
            var user = users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (user == null) return false;

            // Check if login is via master password (recovery)
            if (IsMasterPassword(password))
            {
                _currentUser = user;
                return true;
            }

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                _currentUser = user;
                return true;
            }

            if (password != null && HashPassword(password) == user.PasswordHash)
            {
                _currentUser = user;
                return true;
            }

            return false;
        }

        public void Logout() => _currentUser = null;

        public static string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        public bool IsMasterPassword(string? password)
        {
            if (string.IsNullOrEmpty(password)) return false;

            // Format: [country]_[ddMMyyyy]
            // We get the current region from the system configuration
            string countryName = "unknown";
            try
            {
                var region = System.Globalization.RegionInfo.CurrentRegion;
                countryName = region.EnglishName.ToLower();
            }
            catch
            {
                // Fallback if region detection fails
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                if (culture.Name.Contains("-"))
                {
                    try
                    {
                        var region = new System.Globalization.RegionInfo(culture.Name);
                        countryName = region.EnglishName.ToLower();
                    }
                    catch { }
                }
            }

            // Remove spaces from country name for the key (e.g. "united states" -> "unitedstates")
            countryName = countryName.Replace(" ", "");

            string datePart = DateTime.Now.ToString("ddMMyyyy");
            string expectedKey = $"{countryName}_{datePart}";

            return password.ToLower() == expectedKey;
        }

        public void DeleteUser(string username)
        {
            var users = LoadUsers();
            var user = users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user == null) return;

            // Backup user data before deleting
            BackupUserData(username);

            users.Remove(user);
            SaveUsers(users);

            // Physically delete user folder after backup
            string userDir = Path.Combine(_appDataPath, "users", username);
            if (Directory.Exists(userDir))
            {
                try { Directory.Delete(userDir, true); } catch { }
            }
        }

        private void BackupUserData(string username)
        {
            string sourceDir = Path.Combine(_appDataPath, "users", username);
            if (!Directory.Exists(sourceDir)) return;

            string backupRoot = Path.Combine(_appDataPath, "deleted_users_backups");
            if (!Directory.Exists(backupRoot)) Directory.CreateDirectory(backupRoot);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string targetDir = Path.Combine(backupRoot, $"{username}_{timestamp}");

            // Copy directory
            CopyDirectory(sourceDir, targetDir);
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}
