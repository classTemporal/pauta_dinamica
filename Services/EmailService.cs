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
        private readonly DateTokenService _dates;

        /// <summary>
        /// Crea el servicio de correo usando el reloj del sistema para las fechas dinámicas.
        /// </summary>
        public EmailService() : this(new DateTokenService()) { }

        /// <summary>
        /// Crea el servicio de correo con una fuente de fechas inyectada (para pruebas).
        /// </summary>
        /// <param name="dates">Servicio de tokens de fecha dinámica.</param>
        public EmailService(DateTokenService dates)
        {
            _dates = dates ?? throw new ArgumentNullException(nameof(dates));
        }

        /// <summary>
        /// Resuelve los tokens de fecha dinámica de una plantilla.
        /// Siempre activos: si el texto trae [Hoy], [Semana], [Mes], [Año] o [Rango]
        /// (y sus variantes), se resuelven con la fecha del sistema al enviar.
        /// Sin tokens, el texto vuelve intacto.
        /// </summary>
        private string ResolveDynamicDates(string? text, PautaSchema pauta)
            => _dates.ResolveTokens(text, pauta.DynamicDates, true);

        /// <summary>
        /// Resuelve los placeholders de una plantilla de correo.
        /// Reconoce <c>[Etiqueta]</c> de campo y <c>[Fecha]</c> (fecha del registro).
        ///
        /// <para>
        /// Codificación: cuando la plantilla destino es HTML (método Outlook), los VALORES
        /// inyectados se codifican con <see cref="System.Net.WebUtility.HtmlEncode"/> para que
        /// caracteres como <c>&amp;</c>, <c>&lt;</c> o <c>&gt;</c> no rompan el marcado. Sin esto,
        /// un agente llamado "García &amp; Hijos &lt;S.A.&gt;" produce HTML inválido y Outlook
        /// muestra el cuerpo vacío o truncado.
        /// </para>
        /// <para>
        /// La codificación se aplica SOLO sobre el valor, nunca sobre la plantilla, para
        /// conservar intacto el HTML que el usuario escribió en el editor.
        /// </para>
        /// </summary>
        public string ProcessTemplate(string template, AuditEntry entry, List<FieldDefinition> fields, IEnumerable<EmailReplacementRule>? rules = null, bool encodeValues = false)
        {
            if (string.IsNullOrWhiteSpace(template)) return "";

            string result = template;

            // Codifica solo cuando el destino es HTML. En texto plano (mailto) se conserva
            // el comportamiento original para no mostrar entidades al usuario.
            string Clean(string? value) => encodeValues ? System.Net.WebUtility.HtmlEncode(value ?? "") : (value ?? "");

            // 1. Reemplazar [Fecha]
            result = result.Replace("[Fecha]", Clean(entry.Timestamp.ToString("dd/MM/yyyy HH:mm")), StringComparison.OrdinalIgnoreCase);

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
                        // La comparación de reglas usa el valor crudo; la codificación se
                        // aplica solo al insertar, para no alterar el matching.
                        result = result.Replace(placeholder, Clean(value), StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Prepara y abre el correo en el cliente configurado (Outlook o mailto).
        /// Devuelve true si el correo fue entregado al cliente de forma exitosa.
        /// </summary>
        /// <param name="silent">Si es true no se muestra un aviso por destinatario vacío
        /// (útil en envíos múltiples, donde el resumen final informa los fallos).</param>
        /// <param name="failReason">Motivo del fallo cuando devuelve false (null si tuvo éxito).</param>
        public bool SendEmail(AppSettings globalSettings, PautaSchema? pauta, AuditEntry entry, List<FieldDefinition> fields, string? pdfPath, bool silent, out string? failReason)
        {
            failReason = null;
            if (pauta == null) { failReason = "sin pauta"; return false; }

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

            // Tokens de fecha dinámica ([Hoy], [Semana], [Semana:Sab], [Mes], [Año],
            // [Rango]) también en el asunto, igual que en el cuerpo.
            subject = InsertDynamicDates(subject, pauta);

            // El cuerpo depende del método de envío:
            //   - Outlook -> plantilla HTML enriquecida (.HTMLBody, admite formato y tablas).
            //   - Mailto  -> plantilla de texto plano (mailto no puede transportar marcado).
            // Los campos están separados a propósito, de modo que mailto nunca reciba HTML.
            bool useHtml = pauta.EmailMethod == EmailMethod.Outlook;
            string body = ProcessTemplate(
                useHtml ? pauta.EmailBodyHtmlTemplate : pauta.EmailBodyTemplate,
                entry, fields, pauta.EmailReplacementRules,
                encodeValues: useHtml);

            // Tokens de fecha dinámica ([Hoy], [Semana], [Mes], [Año], [Rango]).
            // Se aplican al final, después de los campos. Sin tokens, el texto queda intacto.
            // El resultado ya viene codificado cuando es HTML, así que se inserta tal cual.
            body = InsertDynamicDates(body, pauta);

            // FIX (Card 37): "mensaje olvidado" — si el destinatario (To) quedó vacío, el
            // correo se abría sin destinatario y podía enviarse en blanco. Bloqueamos el envío
            // y avisamos al usuario en lugar de abrir Outlook/mailto sin remitente.
            to = (to ?? "").Trim();
            if (string.IsNullOrWhiteSpace(to))
            {
                failReason = "sin destinatario (To vacío)";
                if (!silent)
                {
                    MessageBoxHelper.Show(
                        "No se pudo determinar el destinatario del correo (To vacío). Verifique que la pauta tenga configurada la plantilla de correo o el Directorio de Contactos.\n\n" +
                        "Si usa Detección Automática, asegúrese de que el agente tenga correo asociado.",
                        "Destinatario no encontrado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return false;
            }

            // Adjuntos de ESTE correo (principal): PDF + campos elegidos en su configuración.
            var attachments = CollectAttachments(pauta.AttachPdfToEmail, pauta.ExcludedAttachmentFieldIds, entry, fields, pdfPath);

            EmailMethod method = pauta.EmailMethod;

            if (method == EmailMethod.Outlook)
            {
                return SendViaOutlook(to, cc, subject, body, attachments, silent, out failReason);
            }

            return SendViaMailto(to, cc, subject, body, silent, out failReason);
        }

        /// <summary>
        /// Prepara y abre un correo adicional disparado por una <see cref="ConditionalEmailRule"/>.
        /// Reutiliza plantillas con placeholders, directorio de contactos, reglas de reemplazo,
        /// método de envío y adjuntos igual que el correo principal.
        /// Devuelve true si el correo fue entregado al cliente de forma exitosa.
        /// </summary>
        /// <param name="failReason">Motivo del fallo cuando devuelve false (null si tuvo éxito).</param>
        public bool SendConditionalEmail(AppSettings globalSettings, PautaSchema? pauta, ConditionalEmailRule rule, AuditEntry entry, List<FieldDefinition> fields, string? pdfPath, bool silent, out string? failReason)
        {
            failReason = null;
            if (pauta == null || rule == null) { failReason = "sin pauta o regla"; return false; }

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
                // La plantilla "Para" propia de la regla tiene prioridad; si está vacía
                // se hereda la del correo principal como respaldo.
                string toTemplate = string.IsNullOrWhiteSpace(rule.ToTemplate) ? pauta.EmailToTemplate : rule.ToTemplate;
                to = ProcessTemplate(toTemplate, entry, fields, pauta.EmailReplacementRules);
            }

            string cc = ProcessTemplate(string.IsNullOrWhiteSpace(rule.CcTemplate) ? pauta.EmailCcTemplate : rule.CcTemplate, entry, fields, pauta.EmailReplacementRules);
            string subject = ProcessTemplate(rule.SubjectTemplate, entry, fields, pauta.EmailReplacementRules);

            // Tokens de fecha dinámica también en el asunto del adicional, igual que el principal.
            subject = InsertDynamicDates(subject, pauta);

            // Igual que el correo principal: el cuerpo sigue el método de la pauta.
            // El cuerpo HTML propio de la regla tiene prioridad; si está vacío se hereda el
            // del correo principal para no dejar el correo adicional sin contenido.
            bool useHtml = pauta.EmailMethod == EmailMethod.Outlook;
            string body = ProcessTemplate(
                useHtml
                    ? (string.IsNullOrWhiteSpace(rule.BodyHtmlTemplate) ? pauta.EmailBodyHtmlTemplate : rule.BodyHtmlTemplate)
                    : rule.BodyTemplate,
                entry, fields, pauta.EmailReplacementRules,
                encodeValues: useHtml);

            // Tokens de fecha dinámica del correo adicional (misma regla que el principal).
            body = InsertDynamicDates(body, pauta);

            to = (to ?? "").Trim();
            if (string.IsNullOrWhiteSpace(to))
            {
                failReason = $"sin destinatario (To vacío, regla '{rule.Name}')";
                if (!silent)
                {
                    MessageBoxHelper.Show(
                        $"No se pudo determinar el destinatario del correo adicional '{rule.Name}' (To vacío). Verifique su plantilla 'Para' o el Directorio de Contactos.",
                        "Destinatario no encontrado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return false;
            }

            // Adjuntos de ESTE correo adicional (configuración propia de la regla,
            // independiente del principal): PDF + campos elegidos en su botón de adjuntos.
            var attachments = CollectAttachments(rule.AttachPdfToEmail, rule.ExcludedAttachmentFieldIds, entry, fields, pdfPath);

            EmailMethod method = pauta.EmailMethod;

            if (method == EmailMethod.Outlook)
            {
                return SendViaOutlook(to, cc, subject, body, attachments, silent, out failReason);
            }

            return SendViaMailto(to, cc, subject, body, silent, out failReason);
        }

        /// <summary>
        /// Recolecta los archivos que acompañarán UN correo según su propia configuración.
        ///
        /// <para>
        /// El correo principal usa la configuración de la pauta y cada correo adicional la
        /// de su regla: el PDF solo si corresponde, y cada campo de adjunto solo si su
        /// propio flag lo permite y su ID no está en excluidos. Sin configuración (todo
        /// incluido) se envía todo, que es el comportamiento histórico.
        /// </para>
        /// </summary>
        private static List<string> CollectAttachments(bool includePdf, System.Collections.Generic.List<string>? excludedIds, AuditEntry entry, List<FieldDefinition> fields, string? pdfPath)
        {
            var attachments = new List<string>();

            if (includePdf && !string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath))
                attachments.Add(pdfPath);

            var excluded = excludedIds;

            if (fields != null)
            {
                foreach (var f in fields.Where(f => f.Type == FieldType.FileAttachment && f.AttachToEmail))
                {
                    if (excluded != null && excluded.Contains(f.Id)) continue;

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

            return attachments;
        }

        private bool SendViaMailto(string to, string cc, string subject, string body, bool silent, out string? failReason)
        {
            failReason = null;
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
                failReason = $"mailto supera el límite ({url.Length} caracteres)";
                // En lote no se pregunta por cada correo: se acumula el fallo en el resumen.
                if (silent) return false;

                // Some Windows shells silently drop mailto URLs over 2000 chars. Warn the
                // user but still attempt — truncated bodies may still be useful.
                var warn = MessageBoxHelper.Show(
                    $"La longitud total del enlace 'mailto' ({url.Length} caracteres) supera el límite recomendado ({mailtoLimit}). " +
                    "El cliente de correo podría no abrirse o mostrar información incompleta. Considere usar el método Outlook en la configuración de la pauta.\n\n¿Desea continuar de todos modos?",
                    "Aviso de longitud mailto",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning, true);
                if (warn == System.Windows.MessageBoxResult.No) return false;
                failReason = null; // El usuario aceptó intentarlo de todos modos.
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                failReason = "mailto: " + ex.Message;
                // En lote no se espamea un diálogo por cada fallo: va al resumen final.
                if (!silent)
                    MessageBoxHelper.Show("No se pudo abrir el cliente de correo predeterminado: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Instancia única de Outlook reutilizada para todo el lote de envíos. Crear una nueva
        // aplicación COM por cada correo provocaba errores intermitentes al enviar varios
        // correos seguidos (modo selección múltiple).
        private static dynamic? _outlookApp;

        private static dynamic GetOutlookApp()
        {
            if (_outlookApp != null)
            {
                try
                {
                    // Verificar que la instancia siga viva (Outlook pudo haberse cerrado)
                    _ = _outlookApp.ProductCode;
                }
                catch
                {
                    _outlookApp = null;
                }
            }

            if (_outlookApp == null)
            {
                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                {
                    throw new Exception("Microsoft Outlook no parece estar instalado o no se pudo crear la instancia COM.");
                }

                _outlookApp = Activator.CreateInstance(outlookType)!;
            }

            return _outlookApp!;
        }

        private bool SendViaOutlook(string to, string cc, string subject, string body, List<string> attachmentPaths, bool silent, out string? failReason)
        {
            failReason = null;
            try
            {
                dynamic mailItem = GetOutlookApp().CreateItem(0); // 0 = olMailItem

                mailItem.To = to;
                mailItem.CC = cc;
                mailItem.Subject = subject;

                // FIX (Card 37): Se enviaba el cuerpo como texto plano vía .Body, por lo que
                // plantillas con HTML (negritas, saltos de línea, comodines) se mostraban
                // como etiquetas literales en Outlook. Se usa HTMLBody cuando el cuerpo contiene
                // etiquetas HTML; si es texto plano se conserva .Body para evitar caracteres escapados.
                if (IsHtml(body))
                {
                    // Outlook DESCarta los data URI del cuerpo: las imágenes insertadas en el
                    // editor se pierden. Se extraen a archivos temporales y se adjuntan con
                    // Content-ID, reescribiendo el src como cid:<id>.
                    var inlined = new List<string>();
                    string htmlBody = body ?? "";
                    string rewritten = ExtractInlineImages(htmlBody, mailItem, inlined);

                    mailItem.HTMLBody = rewritten;

                    try
                    {
                        mailItem.Display();
                    }
                    finally
                    {
                        // El adjunto ya viaja con el mensaje de Outlook; el temporal solo
                        // existía para poder adjuntarlo.
                        foreach (string temp in inlined)
                        {
                            try { if (File.Exists(temp)) File.Delete(temp); }
                            catch (Exception dex)
                            {
                                System.Diagnostics.Debug.WriteLine("no se pudo borrar la imagen temporal: " + dex.Message);
                            }
                        }
                    }
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
                return true;
            }
            catch (Exception ex)
            {
                failReason = "Outlook: " + ex.Message;
                // En lote no se espamea un diálogo por cada fallo: va al resumen final.
                if (!silent)
                    MessageBoxHelper.Show("Error al usar Outlook Interop: " + ex.Message + "\n\nIntente usar el método 'mailto' en la configuración general.", "Error correo", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Sustituye los tokens de fecha dinámica en un cuerpo ya resuelto.
        ///
        /// <para>
        /// A diferencia de <see cref="ProcessTemplate"/>, aquí el valor insertado se codifica
        /// cuando el cuerpo destino es HTML: los rangos generados contienen texto libre
        /// ("03/10/2026 al 09/10/2026") que, aunque hoy no trae caracteres peligrosos, debe
        /// seguir la misma regla de higiene que los valores de campo.
        /// </para>
        /// </summary>
        private string InsertDynamicDates(string body, PautaSchema pauta)
        {
            if (string.IsNullOrEmpty(body)) return body;

            bool isHtml = IsHtml(body);
            string resolved = ResolveDynamicDates(body, pauta);

            // Si no había tokens que resolver, devuelve el original sin tocar.
            if (string.Equals(resolved, body, StringComparison.Ordinal)) return body;

            return isHtml ? System.Net.WebUtility.HtmlEncode(resolved) : resolved;
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

        /// <summary>
        /// Data URI de imagen incrustado en el cuerpo: <c>data:image/png;base64,....</c>.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex InlineImageRegex = new(
            @"data:(?<mime>image/[a-zA-Z0-9.+-]+);base64,(?<data>[A-Za-z0-9+/=\s]+)",
            System.Text.RegularExpressions.RegexOptions.Compiled |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// Tipos de adjunto de Outlook que se usan aquí. El resto del código trabaja con
        /// <c>dynamic</c> (COM interop), así que se declara solo lo necesario.
        /// </summary>
        private static class OlAttachmentType
        {
            /// <summary>El adjunto es una copia del archivo (valor 1 de <c>OlAttachmentType</c>).</summary>
            public const int olByValue = 1;
        }

        /// <summary>
        /// Convierte los <c>data:</c> URI del cuerpo en adjuntos con Content-ID y reescribe el
        /// <c>src</c> como <c>cid:...</c>.
        ///
        /// <para>
        /// Outlook ignora los data URI dentro de <c>HTMLBody</c>, así que las imágenes del
        /// editor desaparecían al enviar. Guardándolas como adjunto con Content-ID e
        /// indicando <c>PR_ATTACH_CONTENT_ID</c> se muestran en línea.
        /// </para>
        /// <para>
        /// Si una imagen no se puede adjuntar se deja el data URI original: el correo se envía
        /// igual y solo falla esa imagen.
        /// </para>
        /// </summary>
        /// <param name="html">Cuerpo HTML original.</param>
        /// <param name="mailItem">Mensaje de Outlook donde se agregan los adjuntos.</param>
        /// <param name="tempFiles">Rutas temporales creadas, para que el llamador las borre.</param>
        private static string ExtractInlineImages(string html, dynamic mailItem, List<string> tempFiles)
        {
            if (string.IsNullOrEmpty(html) || !html.Contains("data:image", StringComparison.OrdinalIgnoreCase))
                return html;

            return InlineImageRegex.Replace(html, match =>
            {
                string data = match.Groups["data"].Value;
                string mime = match.Groups["mime"].Value.ToLowerInvariant();
                string extension = mime switch
                {
                    "image/jpeg" or "image/jpg" => ".jpg",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    "image/bmp" => ".bmp",
                    _ => ".png"
                };

                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(System.Text.RegularExpressions.Regex.Replace(data, @"\s+", ""));
                }
                catch (FormatException)
                {
                    // Base64 corrupto: se deja el data URI tal cual.
                    return match.Value;
                }

                string? tempPath = null;
                try
                {
                    tempPath = Path.Combine(Path.GetTempPath(), "pauta_img_" + Guid.NewGuid().ToString("N") + extension);
                    File.WriteAllBytes(tempPath, bytes);

                    string contentId = Guid.NewGuid().ToString();

                    dynamic attachment = mailItem.Attachments.Add(tempPath, OlAttachmentType.olByValue, 0, null);
                    attachment.PropertyAccessor.SetProperty(
                        "http://schemas.microsoft.com/mapi/proptag/0x3712001F", contentId);

                    tempFiles.Add(tempPath);
                    return "cid:" + contentId;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("imagen inline no adjuntada: " + ex.Message);

                    if (tempPath != null)
                    {
                        try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                        catch (Exception dex)
                        {
                            System.Diagnostics.Debug.WriteLine("no se pudo borrar la imagen temporal: " + dex.Message);
                        }
                    }

                    return match.Value;
                }
            });
        }
    }
}
