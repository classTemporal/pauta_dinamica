using System;
using System.Collections.Generic;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Services
{
    /// <summary>
    /// Mapeo de cabeceras de Excel a campos de la pauta para la importación.
    ///
    /// <para>
    /// La exportación escribe las cabeceras de los <b>presets</b> (<c>CustomHeader</c> o
    /// etiqueta), así que la importación debe reconocerlas todas: presets, config legacy
    /// y etiquetas actuales. Antes solo se miraba la config legacy, por lo que cualquier
    /// columna exportada con un encabezado personalizado de preset se ignoraba en silencio.
    /// </para>
    ///
    /// <para>Clase pura (sin UI) para poder probarse de forma aislada.</para>
    /// </summary>
    public static class ExcelImportMapper
    {
        public const string TimestampHeader = "Fecha de evaluación";
        public const string DurationHeader = "Duración (min)";

        public sealed class HeaderMapResult
        {
            /// <summary>Columna 1-based → FieldId.</summary>
            public Dictionary<int, string> ColumnMap { get; } = new();

            public int TimestampColumn { get; set; } = -1;
            public int DurationColumn { get; set; } = -1;

            /// <summary>Cabeceras que no corresponden a ningún campo (se ignorarán).</summary>
            public List<string> Skipped { get; } = new();

            /// <summary>Cabeceras que duplican un campo ya mapeado (gana la primera).</summary>
            public List<string> Conflicts { get; } = new();
        }

        /// <summary>
        /// Construye el mapeo columna → FieldId a partir de las cabeceras leídas.
        /// </summary>
        /// <param name="headers">Cabeceras en orden; el índice 0 es la columna 1.</param>
        /// <param name="presets">Presets de exportación de la pauta (puede ser <c>null</c>).</param>
        /// <param name="legacyConfig">Configuración legacy de exportación (puede ser <c>null</c>).</param>
        /// <param name="fields">Campos actuales como pares (Id, Label).</param>
        public static HeaderMapResult MapColumns(
            IList<string?> headers,
            IEnumerable<ExportPreset>? presets,
            IEnumerable<ExportColumnConfig>? legacyConfig,
            IEnumerable<(string Id, string Label)>? fields)
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void Register(string? header, string fieldId)
            {
                string h = (header ?? "").Trim();
                if (h.Length == 0 || string.IsNullOrEmpty(fieldId) || index.ContainsKey(h)) return;
                index[h] = fieldId;
            }

            if (presets != null)
            {
                foreach (var preset in presets)
                {
                    if (preset?.Columns == null) continue;
                    foreach (var col in preset.Columns)
                    {
                        if (col == null) continue;
                        Register(col.CustomHeader, col.FieldId);
                        Register(col.OriginalLabel, col.FieldId);
                    }
                }
            }

            if (legacyConfig != null)
            {
                foreach (var col in legacyConfig)
                {
                    if (col == null) continue;
                    Register(col.CustomHeader, col.FieldId);
                    Register(col.OriginalLabel, col.FieldId);
                }
            }

            if (fields != null)
            {
                foreach (var (id, label) in fields)
                    Register(label, id);
            }

            var result = new HeaderMapResult();
            var seen = new Dictionary<string, int>(StringComparer.Ordinal); // FieldId -> primera columna

            for (int i = 0; i < headers.Count; i++)
            {
                int col = i + 1;
                string h = (headers[i] ?? "").Trim();

                if (h.Equals(TimestampHeader, StringComparison.OrdinalIgnoreCase))
                {
                    result.TimestampColumn = col;
                    continue;
                }

                if (h.Equals(DurationHeader, StringComparison.OrdinalIgnoreCase))
                {
                    result.DurationColumn = col;
                    continue;
                }

                if (h.Length == 0)
                {
                    result.Skipped.Add($"columna {col} (sin cabecera)");
                    continue;
                }

                if (!index.TryGetValue(h, out string? fieldId))
                {
                    result.Skipped.Add($"'{h}'");
                    continue;
                }

                if (seen.TryGetValue(fieldId, out int first))
                {
                    result.Conflicts.Add($"'{h}' (columna {col} duplica la columna {first})");
                    continue;
                }

                seen[fieldId] = col;
                result.ColumnMap[col] = fieldId;
            }

            return result;
        }
    }
}
