using System;

namespace PautaDinamicaApp.Models
{
    public class UserModel
    {
        public string Username { get; set; } = "";
        public string? PasswordHash { get; set; } // Null if no password
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string LastPautaId { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }
}
