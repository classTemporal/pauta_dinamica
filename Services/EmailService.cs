using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Services
{
    public class EmailService
    {
        public string ProcessTemplate(string template, AuditEntry entry, List<FieldDefinition> fields, IEnumerable<EmailReplacementRule>? rules = null)
        {
            if (string.IsNullOrWhiteSpace(template)) return "";

            string result = template;

            // 1. Reemplazar [Fecha]
            result = result.Replace("[Fecha]", entry.Timestamp.ToString("dd/MM/yyyy HH:mm"), StringComparison.OrdinalIgnoreCase);

            // 2. Reemplazar placeholders por etiqueta de campo
            if (fields != null)
            {
                foreach (var field in fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Label)) continue;

                    string placeholder = $"[{field.Label}]";

                    if (result.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                    {
                        string value = "";
                        if (entry.Values.TryGetValue(field.Id, out var val) && val != null)
                        {
                            value = val.ToString() ?? "";
                        }

                        // --- REGLAS DE REEMPLAZO (Multi-Campo) ---
                        if (rules != null)
                        {
                            var rule = rules.FirstOrDefault(r =>
                                r.TargetFieldIds != null &&
                                r.TargetFieldIds.Contains(field.Id) &&
                                string.Equals(r.TargetValue, value, StringComparison.OrdinalIgnoreCase));

                            if (rule != null)
                            {
                                value = rule.ReplacementValue;
                            }
                        }
                        // ---------------------------

                        result = result.Replace(placeholder, value, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            return result;
        }

        public void SendEmail(AppSettings globalSettings, PautaSchema? pauta, AuditEntry entry, List<FieldDefinition> fields, string? pdfPath = null)
        {
            if (pauta == null) return;

            string to = "";
            bool directoryFound = false;

            if (pauta.UseAutomatedRecipient && !string.IsNullOrWhiteSpace(pauta.EmailNameFieldId))
            {
                if (entry.Values.TryGetValue(pauta.EmailNameFieldId, out var nameVal) && nameVal != null)
                {
                    string nameText = nameVal.ToString()?.Trim() ?? "";
                    var contact = pauta.RecipientContacts?.FirstOrDefault(c => string.Equals(c.Name?.Trim(), nameText, StringComparison.OrdinalIgnoreCase));
                    if (contact != null && !string.IsNullOrWhiteSpace(contact.Email))
                    {
                        to = contact.Email;
                        directoryFound = true;
                    }
                }
            }

            if (!directoryFound)
            {
                to = ProcessTemplate(pauta.EmailToTemplate, entry, fields, pauta.EmailReplacementRules);
            }

            string cc = ProcessTemplate(pauta.EmailCcTemplate, entry, fields, pauta.EmailReplacementRules);
            string subject = ProcessTemplate(pauta.EmailSubjectTemplate, entry, fields, pauta.EmailReplacementRules);
            string body = ProcessTemplate(pauta.EmailBodyTemplate, entry, fields, pauta.EmailReplacementRules);

            // Collect all attachments
            var attachments = new List<string>();
            if (!string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath)) attachments.Add(pdfPath);

            if (fields != null)
            {
                foreach (var f in fields.Where(f => f.Type == FieldType.FileAttachment && f.AttachToEmail))
                {
                    if (entry.Values.TryGetValue(f.Id, out var val) && val != null)
                    {
                        string strVal = val.ToString() ?? "";
                        var paths = strVal.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var path in paths)
                        {
                            if (File.Exists(path)) attachments.Add(path);
                        }
                    }
                }
            }

            EmailMethod method = pauta.EmailMethod;

            if (method == EmailMethod.Outlook)
            {
                SendViaOutlook(to, cc, subject, body, attachments);
            }
            else
            {
                SendViaMailto(to, cc, subject, body);
            }
        }

        private void SendViaMailto(string to, string cc, string subject, string body)
        {
            string url = $"mailto:{Uri.EscapeDataString(to)}?cc={Uri.EscapeDataString(cc)}&subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";

            if (url.Length > 2000)
            {
                System.Windows.MessageBox.Show("El correo es demasiado largo para el método 'mailto'. Se ha truncado o podría no abrirse. Considere usar Outlook Interop.", "Aviso");
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("No se pudo abrir el cliente de correo predeterminado: " + ex.Message);
            }
        }

        private void SendViaOutlook(string to, string cc, string subject, string body, List<string> attachmentPaths)
        {
            try
            {
                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                {
                    throw new Exception("Microsoft Outlook no parece estar instalado.");
                }

                dynamic outlookApp = Activator.CreateInstance(outlookType)!;
                dynamic mailItem = outlookApp.CreateItem(0); // 0 = olMailItem

                mailItem.To = to;
                mailItem.CC = cc;
                mailItem.Subject = subject;
                mailItem.Body = body;

                if (attachmentPaths != null)
                {
                    foreach (var path in attachmentPaths)
                    {
                        if (System.IO.File.Exists(path))
                        {
                            mailItem.Attachments.Add(path);
                        }
                    }
                }

                mailItem.Display();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error al usar Outlook Interop: " + ex.Message + "\n\nIntente usar el método 'mailto' en la configuración general.", "Error correo");
            }
        }
    }
}
