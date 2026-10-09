using System;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Services
{
    /// <summary>
    /// Rango de fechas ya resuelto, listo para formatear.
    /// </summary>
    public readonly record struct DateRange(DateTime From, DateTime To)
    {
        /// <summary>Duración del rango en días (incluyendo ambos extremos).</summary>
        public int Days => (int)(To.Date - From.Date).TotalDays + 1;
    }

    /// <summary>
    /// Calcula los valores de los tokens de fecha dinámica a partir del reloj del sistema.
    ///
    /// <para>
    /// Estos tokens NO provienen de los datos de la pauta: permiten títulos como
    /// <c>Auditorías de llamadas del 03/10/2026 al 09/10/2026</c> sin escribir la fecha a mano,
    /// ya que se recalculan solos en cada envío.
    /// </para>
    ///
    /// <para>
    /// La fecha base se inyecta en el constructor en lugar de usar <see cref="DateTime.Now"/>
    /// directamente, para poder escribir pruebas unitarias deterministas.
    /// </para>
    /// </summary>
    public class DateTokenService
    {
        /// <summary>Formato corto por defecto usado al presentar fechas.</summary>
        public const string ShortFormat = "dd/MM/yyyy";

        /// <summary>Formato largo en texto, para presentaciones formales.</summary>
        public const string LongFormat = "d \"de\" MMMM \"de\" yyyy";

        private readonly Func<DateTime> _now;

        /// <summary>
        /// Crea el servicio usando el reloj del sistema como fecha base.
        /// </summary>
        public DateTokenService() : this(() => DateTime.Now) { }

        /// <summary>
        /// Crea el servicio con una fuente de fecha base personalizada (para pruebas).
        /// </summary>
        /// <param name="now">Función que devuelve la fecha "actual".</param>
        public DateTokenService(Func<DateTime> now)
        {
            _now = now ?? throw new ArgumentNullException(nameof(now));
        }

        /// <summary>Fecha base actual.</summary>
        public DateTime Today => _now().Date;

        /// <summary>
        /// Token <c>[Hoy]</c>: la fecha actual del sistema.
        /// </summary>
        public string Hoy() => Today.ToString(ShortFormat);

        /// <summary>
        /// Token <c>[Semana]</c>: rango de lunes a domingo de la semana que contiene la fecha base.
        /// </summary>
        public DateRange SemanaRange()
        {
            // Se trata el lunes como primer día de la semana (offset 0).
            DateTime today = Today;
            int offset = ((int)today.DayOfWeek + 6) % 7;   // lunes = 0 ... domingo = 6
            DateTime monday = today.AddDays(-offset);
            DateTime sunday = monday.AddDays(6);
            return new DateRange(monday, sunday);
        }

        /// <summary>Token <c>[Semana]</c> ya formateado.</summary>
        public string Semana(DynamicDateFormat format = DynamicDateFormat.RangeWithAl)
            => Format(SemanaRange(), format);

        /// <summary>
        /// Token <c>[Mes]</c>: rango del primero al último día del mes actual.
        /// </summary>
        public DateRange MesRange()
        {
            DateTime today = Today;
            return new DateRange(
                new DateTime(today.Year, today.Month, 1),
                new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)));
        }

        /// <summary>Token <c>[Mes]</c> ya formateado.</summary>
        public string Mes(DynamicDateFormat format = DynamicDateFormat.RangeWithAl)
            => Format(MesRange(), format);

        /// <summary>
        /// Token <c>[Año]</c>: rango del 1 de enero al 31 de diciembre del año actual.
        /// </summary>
        public DateRange AnioRange()
        {
            DateTime today = Today;
            return new DateRange(new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31));
        }

        /// <summary>Token <c>[Año]</c> ya formateado.</summary>
        public string Anio(DynamicDateFormat format = DynamicDateFormat.RangeWithAl)
            => Format(AnioRange(), format);

        /// <summary>
        /// Token <c>[Rango]</c>: rango custom definido en la configuración de la pauta.
        /// </summary>
        /// <param name="config">Configuración del rango. Si es <c>null</c> se usan los últimos 7 días.</param>
        public DateRange RangoRange(DynamicDateConfig? config)
        {
            config ??= new DynamicDateConfig();
            DateTime today = Today;

            switch (config.Mode)
            {
                case DynamicRangeMode.Fixed:
                    // Sin fechas capturadas se degrada a la ventana de días para no fallar.
                    if (config.FromDate == null || config.ToDate == null)
                        return Ventana(today, config.Days);
                    return new DateRange(config.FromDate.Value.Date, config.ToDate.Value.Date);

                case DynamicRangeMode.Weekday:
                    return VentanaSemanal(today, config.FromDay, config.ToDay);

                case DynamicRangeMode.Week:
                default:
                    return Ventana(today, config.Days);
            }
        }

        /// <summary>Token <c>[Rango]</c> ya formateado.</summary>
        public string Rango(DynamicDateConfig? config, DynamicDateFormat? format = null)
            => Format(RangoRange(config), format ?? config?.Format ?? DynamicDateFormat.RangeWithAl);

        /// <summary>
        /// Token parametrizado <c>[Rango:...]</c>: rango autocontenido, sin depender de la
        /// configuración de la pauta. Permite varios rangos distintos en el mismo correo.
        /// Formatos: <c>[Rango:7d]</c> (últimos N días), <c>[Rango:Sab]</c> (una semana desde
        /// ese día), <c>[Rango:Sab-Vie]</c> (entre dos días, compatible), <c>[Rango:2026-10-03_2026-10-09]</c>.
        /// </summary>
        /// <exception cref="FormatException">Si el parámetro no tiene un formato reconocido.</exception>
        public string RangoParam(string param)
        {
            string p = (param ?? "").Trim();

            var mDays = System.Text.RegularExpressions.Regex.Match(p, @"^(\d+)\s*d?$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mDays.Success && int.TryParse(mDays.Groups[1].Value, out int n))
                return Format(Ventana(Today, n));

            var mFixed = System.Text.RegularExpressions.Regex.Match(p,
                @"^(\d{4}-\d{2}-\d{2})\s*[_-]\s*(\d{4}-\d{2}-\d{2})$");
            if (mFixed.Success
                && DateTime.TryParse(mFixed.Groups[1].Value, out DateTime from)
                && DateTime.TryParse(mFixed.Groups[2].Value, out DateTime to))
                return Format(new DateRange(from.Date, to.Date));

            var mWeek = System.Text.RegularExpressions.Regex.Match(p, @"^([a-záé]+)\s*[-–]\s*([a-záé]+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mWeek.Success)
                return Format(VentanaSemanal(Today, ParseDayEs(mWeek.Groups[1].Value), ParseDayEs(mWeek.Groups[2].Value)));

            // Un solo día: una semana desde ese día.
            var mDay = System.Text.RegularExpressions.Regex.Match(p, @"^([a-záé]+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mDay.Success)
            {
                DayOfWeek start = ParseDayEs(mDay.Groups[1].Value);
                return Format(VentanaSemanal(Today, start, (DayOfWeek)(((int)start + 6) % 7)));
            }

            throw new FormatException($"Parámetro de rango no reconocido: '{param}'. Use [Rango:7d], [Rango:Sab], [Rango:Sab-Vie] o [Rango:2026-10-03_2026-10-09].");
        }

        /// <summary>Abreviatura o nombre de día en español a <see cref="DayOfWeek"/>.</summary>
        /// <exception cref="FormatException">Si no se reconoce el día.</exception>
        public static DayOfWeek ParseDayEs(string day)
        {
            return day.Trim().ToLowerInvariant() switch
            {
                "dom" or "domingo" => DayOfWeek.Sunday,
                "lun" or "lunes" => DayOfWeek.Monday,
                "mar" or "martes" => DayOfWeek.Tuesday,
                "mie" or "miércoles" or "miercoles" => DayOfWeek.Wednesday,
                "jue" or "jueves" => DayOfWeek.Thursday,
                "vie" or "viernes" => DayOfWeek.Friday,
                "sab" or "sábado" or "sabado" => DayOfWeek.Saturday,
                _ => throw new FormatException($"Día no reconocido: '{day}'.")
            };
        }

        /// <summary>Abreviatura en español de un día (<c>Sab</c>, <c>Vie</c>...).</summary>
        public static string DayAbbrev(DayOfWeek day) => day switch
        {
            DayOfWeek.Sunday => "Dom",
            DayOfWeek.Monday => "Lun",
            DayOfWeek.Tuesday => "Mar",
            DayOfWeek.Wednesday => "Mie",
            DayOfWeek.Thursday => "Jue",
            DayOfWeek.Friday => "Vie",
            DayOfWeek.Saturday => "Sab",
            _ => day.ToString()
        };

        /// <summary>
        /// Ventana de <paramref name="days"/> días terminando hoy (incluido).
        /// </summary>
        private static DateRange Ventana(DateTime today, int days)
        {
            // Acota a [1, 3660] para evitar desbordes con valores absurdos.
            int n = days < 1 ? 1 : (days > 3660 ? 3660 : days);
            return new DateRange(today.AddDays(-(n - 1)), today);
        }

        /// <summary>
        /// Rango del día <paramref name="fromDay"/> al día <paramref name="toDay"/> de la semana.
        ///
        /// <para>
        /// Semántica retrospectiva: devuelve el bloque más reciente que YA CERRÓ en
        /// <paramref name="toDay"/>. Es lo que necesita una auditoría semanal, que reporta
        /// el periodo terminado y no uno en curso.
        /// </para>
        /// <para>
        /// Ejemplos con hoy = viernes 09/10/2026:
        /// <list type="bullet">
        ///   <item><description>Sábado → viernes: <c>03/10 al 09/10</c> (cierra hoy)</description></item>
        ///   <item><description>Domingo → sábado: <c>27/09 al 03/10</c> (el bloque 04/10→10/10 aún no cierra)</description></item>
        /// </list>
        /// El orden invertido (ej: sábado → viernes) cruza el fin de semana sin problema.
        /// </para>
        /// </summary>
        private static DateRange VentanaSemanal(DateTime today, DayOfWeek fromDay, DayOfWeek toDay)
        {
            // Distancia hacia atrás desde hoy hasta el último 'toDay' ocurrido (incluido hoy).
            int backTo = ((int)today.DayOfWeek - (int)toDay + 7) % 7;
            DateTime to = today.AddDays(-backTo);

            // Longitud del bloque: de fromDay a toDay, inclusive.
            int span = ((int)toDay - (int)fromDay + 7) % 7;
            DateTime from = to.AddDays(-span);

            return new DateRange(from, to);
        }

        /// <summary>
        /// Aplica el formato elegido al rango.
        /// </summary>
        public static string Format(DateRange range, DynamicDateFormat format = DynamicDateFormat.RangeWithAl)
        {
            // Normaliza para que From nunca sea posterior a To (protege el formateo).
            // DateRange es de solo lectura, así que se reconstruye en vez de mutarse.
            DateTime from = range.From <= range.To ? range.From : range.To;
            DateTime to = range.From <= range.To ? range.To : range.From;

            return format switch
            {
                DynamicDateFormat.RangeWithDash => $"{from.ToString(ShortFormat)} - {to.ToString(ShortFormat)}",
                DynamicDateFormat.EndOnly => to.ToString(ShortFormat),
                DynamicDateFormat.EndLong => to.ToString(LongFormat),
                DynamicDateFormat.RangeWithAl => $"{from.ToString(ShortFormat)} al {to.ToString(ShortFormat)}",
                _ => $"{from.ToString(ShortFormat)} al {to.ToString(ShortFormat)}"
            };
        }

        /// <summary>
        /// Reemplaza los tokens de fecha dinámica presentes en el texto.
        /// Se aplica DESPUÉS de resolver los campos de la pauta, para que una etiqueta que
        /// contenga algo parecido a un token no se interprete como fecha.
        /// </summary>
        /// <param name="text">Texto donde buscar los tokens.</param>
        /// <param name="config">Configuración de la pauta para el token [Rango].</param>
        /// <param name="enabled">
        /// Si es <c>false</c> no se sustituye nada y el texto se devuelve tal cual.
        /// </param>
        public string ResolveTokens(string? text, DynamicDateConfig? config, bool enabled = true)
        {
            if (!enabled || string.IsNullOrEmpty(text)) return text ?? "";

            string result = text;

            // Parametrizados primero: [Rango:7d], [Rango:Sab-Vie]... Si uno no se
            // entiende se deja tal cual en lugar de romper el envío.
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\[Rango:([^\]]+)\]",
                m =>
                {
                    try { return RangoParam(m.Groups[1].Value); }
                    catch (FormatException) { return m.Value; }
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (Contains(result, "[Rango]")) result = Replace(result, "[Rango]", Rango(config));
            // [Semana:Lun]: una semana desde el día indicado.
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\[Semana:([^\]]+)\]",
                m =>
                {
                    try
                    {
                        DayOfWeek start = ParseDayEs(m.Groups[1].Value);
                        return Format(VentanaSemanal(Today, start, (DayOfWeek)(((int)start + 6) % 7)));
                    }
                    catch (FormatException) { return m.Value; }
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (Contains(result, "[Semana]")) result = Replace(result, "[Semana]", Semana(config?.Format ?? DynamicDateFormat.RangeWithAl));
            if (Contains(result, "[Mes]")) result = Replace(result, "[Mes]", Mes(config?.Format ?? DynamicDateFormat.RangeWithAl));
            if (Contains(result, "[Año]") || Contains(result, "[Anio]"))
            {
                result = Replace(result, "[Año]", Anio(config?.Format ?? DynamicDateFormat.RangeWithAl));
                result = Replace(result, "[Anio]", Anio(config?.Format ?? DynamicDateFormat.RangeWithAl));
            }
            if (Contains(result, "[Hoy]")) result = Replace(result, "[Hoy]", Hoy());

            return result;
        }

        private static bool Contains(string text, string token)
            => text.Contains(token, StringComparison.OrdinalIgnoreCase);

        private static string Replace(string text, string token, string value)
            => text.Replace(token, value, StringComparison.OrdinalIgnoreCase);
    }
}
