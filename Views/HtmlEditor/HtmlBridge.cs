using System;
using System.Runtime.InteropServices;

namespace PautaDinamicaApp.Views.HtmlEditor
{
    /// <summary>
    /// Mensaje recibido desde el editor HTML alojado en el WebView2.
    /// </summary>
    public enum EditorMessageKind
    {
        /// <summary>El DOM terminó de inicializarse y ya acepta contenido.</summary>
        Ready,

        /// <summary>El contenido cambió; <see cref="EditorMessage.Html"/> trae el marcado nuevo.</summary>
        Changed,

        /// <summary>Altura del contenido; útil para ajustar el control.</summary>
        Size,

        /// <summary>El usuario pidió insertar un campo dinámico de la pauta.</summary>
        InsertField,

        /// <summary>El usuario pidió insertar un token de fecha dinámica.</summary>
        InsertDate
    }

    /// <summary>
    /// Mensaje enviado por el JavaScript del editor hacia el host WPF.
    /// </summary>
    public sealed class EditorMessage
    {
        /// <summary>Tipo de mensaje.</summary>
        public EditorMessageKind Kind { get; init; }

        /// <summary>Contenido HTML del editor (solo en <see cref="EditorMessageKind.Changed"/>).</summary>
        public string? Html { get; init; }

        /// <summary>Altura reportada del contenido (solo en <see cref="EditorMessageKind.Size"/>).</summary>
        public double Height { get; init; }
    }

    /// <summary>
    /// Puente COM que el JavaScript del editor invoca para enviar mensajes al host WPF.
    ///
    /// <para>
    /// WebView2 solo permite llamadas de JS hacia el host a través de un objeto
    /// <c>[ComVisible(true)]</c> registrado con <c>AddHostObjectToScript</c>. El nombre con el
    /// que se registra debe coincidir con el que usa el HTML (ver <c>editor.html</c>).
    /// </para>
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class HtmlBridge
    {
        /// <summary>
        /// Se dispara cada vez que el editor notifica un cambio al host.
        /// </summary>
        public event Action<EditorMessage>? MessageReceived;

        /// <summary>
        /// Punto de entrada invocado desde JavaScript vía <c>postMessage</c>.
        /// </summary>
        /// <param name="payload">JSON serializado por el editor.</param>
        public void PostMessage(string payload)
        {
            var message = Parse(payload);
            if (message != null)
            {
                MessageReceived?.Invoke(message);
            }
        }

        /// <summary>
        /// Convierte el JSON del editor en un <see cref="EditorMessage"/> tipado.
        /// Se parsea a mano para no acoplar el control a un serializador concreto.
        /// </summary>
        internal static EditorMessage? Parse(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;

            // Formato emitido por editor.html: {"type":"...","html":"...","height":123}
            string type = ReadString(payload, "type") ?? "";

            return type.ToLowerInvariant() switch
            {
                "ready" => new EditorMessage { Kind = EditorMessageKind.Ready },
                "changed" => new EditorMessage
                {
                    Kind = EditorMessageKind.Changed,
                    Html = ReadString(payload, "html") ?? ""
                },
                "size" => new EditorMessage
                {
                    Kind = EditorMessageKind.Size,
                    Height = ReadNumber(payload, "height")
                },
                "insertfield" => new EditorMessage { Kind = EditorMessageKind.InsertField },
                "insertdate" => new EditorMessage { Kind = EditorMessageKind.InsertDate },
                _ => null
            };
        }

        /// <summary>Extrae el valor de una propiedad de texto del JSON.</summary>
        private static string? ReadString(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int at = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;

            int colon = json.IndexOf(':', at + needle.Length);
            if (colon < 0) return null;

            int start = json.IndexOf('"', colon + 1);
            if (start < 0) return null;

            var sb = new System.Text.StringBuilder();
            for (int i = start + 1; i < json.Length; i++)
            {
                char c = json[i];

                if (c == '\\' && i + 1 < json.Length)
                {
                    // Secuencias de escape habituales en HTML.
                    char next = json[++i];
                    sb.Append(next switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        'b' => '\b',
                        'f' => '\f',
                        _ => next
                    });
                    continue;
                }

                if (c == '"') break;
                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>Extrae el valor numérico de una propiedad del JSON.</summary>
        private static double ReadNumber(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int at = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return 0;

            int colon = json.IndexOf(':', at + needle.Length);
            if (colon < 0) return 0;

            int i = colon + 1;
            while (i < json.Length && (char.IsWhiteSpace(json[i]))) i++;

            int startNum = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-')) i++;

            if (i == startNum) return 0;

            return double.TryParse(json.Substring(startNum, i - startNum),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double value)
                ? value
                : 0;
        }
    }
}
