using System;

namespace PautaDinamicaApp.Models
{
    public class MessageTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
    }
}
