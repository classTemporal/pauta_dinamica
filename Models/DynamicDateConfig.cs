using System;

namespace PautaDinamicaApp.Models
{
    /// <summary>
    /// Modo de cálculo del rango de fechas dinámico ([Rango]).
    /// El valor se deriva del reloj del sistema al momento de generar el correo/PDF,
    /// NO de los datos de la pauta.
    /// </summary>
    public enum DynamicRangeMode
    {
        /// <summary>Ventana de <see cref="DynamicDateConfig.Days"/> días terminando hoy (incluido).</summary>
        Week,

        /// <summary>Desde <see cref="DynamicDateConfig.FromDate"/> hasta <see cref="DynamicDateConfig.ToDate"/>.</summary>
        Fixed,

        /// <summary>Del día <see cref="DynamicDateConfig.FromDay"/> al día <see cref="DynamicDateConfig.ToDay"/> de la semana.</summary>
        Weekday
    }

    /// <summary>
    /// Formato de salida para los rangos de fecha. Evita que el usuario escriba
    /// cadenas de formato a mano y reduce errores de presentación.
    /// </summary>
    public enum DynamicDateFormat
    {
        /// <summary><c>03/10/2026 al 09/10/2026</c></summary>
        RangeWithAl,

        /// <summary><c>03/10/2026 - 09/10/2026</c></summary>
        RangeWithDash,

        /// <summary><c>03/10/2026</c> (solo la fecha final del rango)</summary>
        EndOnly,

        /// <summary><c>3 de octubre de 2026</c> (solo la fecha final, en texto)</summary>
        EndLong
    }

    /// <summary>
    /// Configuración de las fechas dinámicas de una pauta.
    ///
    /// <para>
    /// Permite componer títulos como
    /// <c>Auditorías de llamadas del 03/10/2026 al 09/10/2026</c> sin escribir la fecha a mano:
    /// el valor se recalcula solo con la fecha del sistema cada vez que se envía un correo
    /// o se genera un PDF.
    /// </para>
    ///
    /// <para>
    /// Es totalmente opcional: si <c>PautaSchema.UseDynamicDates</c> es <c>false</c> los tokens
    /// de fecha dinámica no se resuelven y el comportamiento existente no cambia.
    /// </para>
    /// </summary>
    public class DynamicDateConfig
    {
        /// <summary>Modo de cálculo del rango. Por defecto, entre dos días de la semana.</summary>
        public DynamicRangeMode Mode { get; set; } = DynamicRangeMode.Weekday;

        /// <summary>
        /// Tamaño de la ventana en días cuando <see cref="Mode"/> es <see cref="DynamicRangeMode.Week"/>.
        /// El rango termina hoy (incluido) y abarca <see cref="Days"/> días en total.
        /// </summary>
        public int Days { get; set; } = 7;

        /// <summary>Inicio del rango cuando <see cref="Mode"/> es <see cref="DynamicRangeMode.Fixed"/>.</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Fin del rango cuando <see cref="Mode"/> es <see cref="DynamicRangeMode.Fixed"/>.</summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Día de la semana que abre el rango cuando <see cref="Mode"/> es
        /// <see cref="DynamicRangeMode.Weekday"/> (ej: <see cref="DayOfWeek.Saturday"/>).
        /// </summary>
        public DayOfWeek FromDay { get; set; } = DayOfWeek.Saturday;

        /// <summary>
        /// Día de la semana que cierra el rango cuando <see cref="Mode"/> es
        /// <see cref="DynamicRangeMode.Weekday"/> (ej: <see cref="DayOfWeek.Friday"/>).
        /// </summary>
        public DayOfWeek ToDay { get; set; } = DayOfWeek.Friday;

        /// <summary>Formato de presentación del rango.</summary>
        public DynamicDateFormat Format { get; set; } = DynamicDateFormat.RangeWithAl;

        /// <summary>
        /// Copia profunda de la configuración.
        ///
        /// <para>
        /// Se usa al duplicar una pauta: si la copia compartiera la instancia, editar las
        /// fechas de la copia modificaría también el original.
        /// </para>
        /// </summary>
        public DynamicDateConfig Clone() => new()
        {
            Mode = Mode,
            Days = Days,
            FromDate = FromDate,
            ToDate = ToDate,
            FromDay = FromDay,
            ToDay = ToDay,
            Format = Format
        };
    }
}
