using System;

namespace PautaDinamicaApp.Models
{
    /// <summary>
    /// Valor predeterminado de un campo de tipo <see cref="FieldType.Date"/>.
    ///
    /// <para>
    /// Además de una fecha fija (<c>dd/MM/yyyy</c>) y del dinámico <c>TODAY</c> (hoy),
    /// soporta un día fijo de cada mes con el formato <c>MONTHDAY:N</c> (N = 1..31).
    /// Ejemplo: <c>MONTHDAY:5</c> se resuelve cada mes al día 5 del mes en curso,
    /// sin tener que editar la pauta manualmente.
    /// </para>
    /// </summary>
    public static class DateDefaultValue
    {
        public const string TodayToken = "TODAY";
        public const string MonthDayPrefix = "MONTHDAY:";

        /// <summary>
        /// Resuelve el valor predeterminado a la fecha concreta (<c>dd/MM/yyyy</c>)
        /// que debe mostrarse al crear un registro.
        /// </summary>
        /// <param name="defaultValue">Contenido de <c>FieldDefinition.DefaultValue</c>.</param>
        /// <param name="now">Fecha base (normalmente hoy). Si es <c>null</c> se usa hoy.</param>
        /// <returns>Fecha en formato <c>dd/MM/yyyy</c>, o el texto original si no se reconoce.</returns>
        public static string Resolve(string? defaultValue, DateTime? now = null)
        {
            if (string.IsNullOrWhiteSpace(defaultValue))
                return string.Empty;

            string v = defaultValue.Trim();
            DateTime baseDate = (now ?? DateTime.Now).Date;

            if (v.Equals(TodayToken, StringComparison.OrdinalIgnoreCase))
                return baseDate.ToString("dd/MM/yyyy");

            if (TryParseMonthDay(v, out int day))
                return ResolveMonthDay(day, baseDate).ToString("dd/MM/yyyy");

            if (DateTime.TryParse(v, out DateTime fixedDate))
                return fixedDate.ToString("dd/MM/yyyy");

            return v;
        }

        /// <summary>
        /// Calcula la fecha concreta del día fijo mensual para el mes de <paramref name="baseDate"/>.
        /// Si el mes no tiene tantos días (ej: día 30 en febrero) se ajusta al último día del mes.
        /// </summary>
        public static DateTime ResolveMonthDay(int day, DateTime? baseDate = null)
        {
            DateTime today = (baseDate ?? DateTime.Now).Date;
            int clamped = Math.Clamp(day, 1, DateTime.DaysInMonth(today.Year, today.Month));
            return new DateTime(today.Year, today.Month, clamped);
        }

        /// <summary>
        /// Intenta extraer el día (1..31) de un token <c>MONTHDAY:N</c>.
        /// </summary>
        public static bool TryParseMonthDay(string? value, out int day)
        {
            day = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string v = value.Trim();
            if (!v.StartsWith(MonthDayPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string num = v.Substring(MonthDayPrefix.Length).Trim();
            if (!int.TryParse(num, out day))
                return false;

            return day >= 1 && day <= 31;
        }

        /// <summary>Indica si el valor es dinámico (se recalcula solo: TODAY o MONTHDAY:N).</summary>
        public static bool IsDynamic(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (value.Trim().Equals(TodayToken, StringComparison.OrdinalIgnoreCase))
                return true;
            return TryParseMonthDay(value, out _);
        }

        /// <summary>
        /// Texto amigable para mostrar en la columna "Valor predeterminado" del diseñador.
        /// Ej: <c>MONTHDAY:5</c> → <c>Día 5 de cada mes</c>.
        /// </summary>
        public static string Describe(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "(Fecha)";

            string v = value.Trim();
            if (v.Equals(TodayToken, StringComparison.OrdinalIgnoreCase))
                return "Hoy (dinámico)";

            if (TryParseMonthDay(v, out int day))
                return $"Día {day} de cada mes";

            return v;
        }
    }
}
