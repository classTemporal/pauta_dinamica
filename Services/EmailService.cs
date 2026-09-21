using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using PautaDinamicaApp;
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

            // FIX (Card 37): "mensaje olvidado" — si el destinatario (To) quedó vacío, el
            // correo se abría sin destinatario y podía enviarse en blanco. Bloqueamos el envío
            // y avisamos al usuario en lugar de abrir Outlook/mailto sin remitente.
            to = (to ?? "").Trim();
            if (string.IsNullOrWhiteSpace(to))
            {
                MessageBoxHelper.Show(
                    "No se pudo determinar el destinatario del correo (To vacío). Verifique que la pauta tenga configurada la plantilla de correo o el Directorio de Contactos.\n\n" +
                    "Si usa Detección Automática, asegúrese de que el agente tenga correo asociado.",
                    "Destinatario no encontrado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

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
            // RFC 6068: line breaks inside mailto body must be CRLF encoded as %0D%0A.
            // Uri.EscapeDataString maps '\n' -> '%0A' which some clients ignore, so we
            // normalize newlines to CRLF before encoding to preserve paragraphs.
            string NormalizeLineBreaks(string s) => s?.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n") ?? "";

            string url = "mailto:" + Uri.EscapeDataString(to) +
                         "?cc=" + Uri.EscapeDataString(NormalizeLineBreaks(cc)) +
                         "&subject=" + Uri.EscapeDataString(NormalizeLineBreaks(subject)) +
                         "&body=" + Uri.EscapeDataString(NormalizeLineBreaks(body));

            const int mailtoLimit = 2000;
            if (url.Length > mailtoLimit)
            {
                // Some Windows shells silently drop mailto URLs over 2000 chars. Warn the
                // user but still attempt — truncated bodies may still be useful.
                var warn = MessageBoxHelper.Show(
                    $"La longitud total del enlace 'mailto' ({url.Length} caracteres) supera el límite recomendado ({mailtoLimit}). " +
                    "El cliente de correo podría no abrirse o mostrar información incompleta. Considere usar el método Outlook en la configuración de la pauta.\n\n¿Desea continuar de todos modos?",
                    "Aviso de longitud mailto",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning, true);
                if (warn == System.Windows.MessageBoxResult.No) return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Show("No se pudo abrir el cliente de correo predeterminado: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendViaOutlook(string to, string cc, string subject, string body, List<string> attachmentPaths)
        {
            try
            {
                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                {
                    throw new Exception("Microsoft Outlook no parece estar instalado o no se pudo crear la instancia COM.");
                }

                dynamic outlookApp = Activator.CreateInstance(outlookType)!;
                dynamic mailItem = outlookApp.CreateItem(0); // 0 = olMailItem

                mailItem.To = to;
                mailItem.CC = cc;
                mailItem.Subject = subject;

                // FIX (Card 37): Se enviaba el cuerpo como texto plano vía .Body, por lo que
                // plantillas con HTML (negritas, saltos de línea, comodines) se mostraban
                // como etiquetas literales en Outlook. Se usa HTMLBody cuando el cuerpo contiene
                // etiquetas HTML; si es texto plano se conserva .Body para evitar caracteres escapados.
                if (IsHtml(body))
                {
                    mailItem.HTMLBody = body;
                }
                else
                {
                    mailItem.Body = body;
                }

                if (attachmentPaths != null)
                {
                    foreach (var path in attachmentPaths)
                    {
                        if (System.IO.File.Exists(path))
                        {
                            try
                            {
                                mailItem.Attachments.Add(path);
                            }
                            catch (Exception aex)
                            {
                                // Un adjunto inaccesible no debe abortar el envío del correo.
                                System.Diagnostics.Debug.WriteLine($"Error adjuntando '{path}': {aex.Message}");
                            }
                        }
                    }
                }

                mailItem.Display();
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Show("Error al usar Outlook Interop: " + ex.Message + "\n\nIntente usar el método 'mailto' en la configuración general.", "Error correo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Determina si un cuerpo de correo contiene etiquetas HTML para decidir entre .Body y .HTMLBody.
        /// </summary>
        private static bool IsHtml(string? content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;
            string lower = content.ToLowerInvariant();
            return lower.Contains("<html") || lower.Contains("<body") ||
                   lower.Contains("<br") || lower.Contains("<p>") || lower.Contains("<div") ||
                   lower.Contains("<table") || lower.Contains("<a ") || lower.Contains("<b>") || lower.Contains("<strong");
        }
    }
}
