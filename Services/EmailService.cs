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

            Console.WriteLine($"DEBUG: --- START SEND EMAIL (Pauta: {pauta.Name}) ---");
            Console.WriteLine($"DEBUG: UseAutomatedRecipient prop: {pauta.UseAutomatedRecipient}");

            // 1. PRIORIDAD 1: DIRECTORIO (Solo si está ACTIVO en la pauta)
            if (pauta.UseAutomatedRecipient && !string.IsNullOrWhiteSpace(pauta.EmailNameFieldId))
            {
                if (entry.Values.TryGetValue(pauta.EmailNameFieldId, out var nameVal) && nameVal != null)
                {
                    string nameText = nameVal.ToString()?.Trim() ?? "";
                    Console.WriteLine($"DEBUG: Directory search for name: '{nameText}'");
                    var contact = pauta.RecipientContacts?.FirstOrDefault(c => string.Equals(c.Name?.Trim(), nameText, StringComparison.OrdinalIgnoreCase));
                    if (contact != null && !string.IsNullOrWhiteSpace(contact.Email))
                    {
                        to = contact.Email;
                        directoryFound = true;
                        Console.WriteLine($"DEBUG: Found match in Directory: {to}");
                    }
                    else { Console.WriteLine($"DEBUG: No match found in Directory for '{nameText}'"); }
                }
                else { Console.WriteLine($"DEBUG: Source field {pauta.EmailNameFieldId} not found in record."); }
            }

            // 2. PRIORIDAD 2: CAMPO MANUAL DE LA PAUTA
            // Si no se encontró en el directorio (porque falló o estaba desactivado), usamos el template de la pauta.
            if (!directoryFound)
            {
                string pautaManual = ProcessTemplate(pauta.EmailToTemplate, entry, fields, pauta.EmailReplacementRules);
                if (!string.IsNullOrWhiteSpace(pautaManual))
                {
                    to = pautaManual;
                    Console.WriteLine($"DEBUG: Using Pauta Manual template: {to}");
                }
                else
                {
                    // 3. PRIORIDAD 3: FALLBACK GLOBAL
                    // Solo si la pauta no tiene nada, usamos la configuración general.
                    // (Las reglas de la pauta también aplican al global si se usa como fallback para esta pauta)
                    to = ProcessTemplate(globalSettings.EmailToTemplate, entry, fields, pauta.EmailReplacementRules);
                    Console.WriteLine($"DEBUG: Using Global Fallback template: {to}");
                }
            }

            // Procesar el resto de campos (CC, Asunto, Cuerpo) con herencia simple (Pauta > Global)
            string cc = ProcessTemplate(!string.IsNullOrWhiteSpace(pauta.EmailCcTemplate) ? pauta.EmailCcTemplate : globalSettings.EmailCcTemplate, entry, fields, pauta.EmailReplacementRules);
            string subject = ProcessTemplate(!string.IsNullOrWhiteSpace(pauta.EmailSubjectTemplate) ? pauta.EmailSubjectTemplate : globalSettings.EmailSubjectTemplate, entry, fields, pauta.EmailReplacementRules);
            string body = ProcessTemplate(!string.IsNullOrWhiteSpace(pauta.EmailBodyTemplate) ? pauta.EmailBodyTemplate : globalSettings.EmailBodyTemplate, entry, fields, pauta.EmailReplacementRules);

            // Método de envío: Si la pauta no tiene una configuración explícita (pauta.EmailMethod == globalSettings.SelectedEmailMethod es un chequeo débil, 
            // pero como no hay un valor 'Inherit', usaremos el de la pauta si se cambió de Mailto, o el global como base)
            EmailMethod method = pauta.EmailMethod;

            // Si la pauta tiene el default (Mailto) pero el global es Outlook, priorizamos el global si el usuario lo configuró así.
            // Para ser más precisos, si el global es diferente de Mailto y la pauta sigue en Mailto, usamos el global.
            if (pauta.EmailMethod == EmailMethod.Mailto && globalSettings.SelectedEmailMethod != EmailMethod.Mailto)
            {
                method = globalSettings.SelectedEmailMethod;
            }

            Console.WriteLine($"DEBUG: Final 'To': '{to}'");
            Console.WriteLine($"DEBUG: Final Method: {method}");

            if (method == EmailMethod.Outlook)
            {
                SendViaOutlook(to, cc, subject, body, pdfPath);
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

        private void SendViaOutlook(string to, string cc, string subject, string body, string? attachmentPath)
        {
            try
            {
                // Uso de COM dinámico para evitar dependencia estricta de versión de Interop
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

                if (!string.IsNullOrEmpty(attachmentPath) && System.IO.File.Exists(attachmentPath))
                {
                    mailItem.Attachments.Add(attachmentPath);
                }

                mailItem.Display(); // Mostramos el correo para que el usuario sea quien de click en Enviar
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error al usar Outlook Interop: " + ex.Message + "\n\nIntente usar el método 'mailto' en la configuración general.", "Error correo");
            }
        }
    }
}
