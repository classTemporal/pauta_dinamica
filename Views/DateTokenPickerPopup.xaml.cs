using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;

namespace PautaDinamicaApp.Views
{
    /// <summary>
    /// Selector de tokens de fecha dinámica para insertar en el cuerpo del correo.
    ///
    /// <para>
    /// Los marcadores <c>[Hoy]</c>, <c>[Semana]</c>, <c>[Mes]</c> y <c>[Año]</c> se insertan
    /// directamente. <c>[Rango]</c> admite configuración (modo y formato) que se aplica sobre
    /// la pauta al aceptar, de modo que la vista previa coincida con lo que se enviará.
    /// </para>
    /// </summary>
    public partial class DateTokenPickerPopup : Window
    {
        /// <summary>Marcador elegido, por ejemplo <c>[Rango]</c>. Vacío si se canceló.</summary>
        public string SelectedToken { get; private set; } = string.Empty;

        /// <summary>Configuración resultante del rango; <c>null</c> si no se modificó.</summary>
        public DynamicDateConfig? ResultingConfig { get; private set; }

        private readonly DynamicDateConfig _initial;

        public DateTokenPickerPopup(DynamicDateConfig? current = null)
        {
            InitializeComponent();

            _initial = current ?? new DynamicDateConfig();

            // Días de la semana en español, con el valor del enum en el Tag.
            var dayNames = new (DayOfWeek Day, string Name)[]
            {
                (DayOfWeek.Monday, "Lunes"),
                (DayOfWeek.Tuesday, "Martes"),
                (DayOfWeek.Wednesday, "Miércoles"),
                (DayOfWeek.Thursday, "Jueves"),
                (DayOfWeek.Friday, "Viernes"),
                (DayOfWeek.Saturday, "Sábado"),
                (DayOfWeek.Sunday, "Domingo")
            };

            foreach (var d in dayNames)
            {
                FromDayCombo.Items.Add(new ComboBoxItem { Content = d.Name, Tag = d.Day });
                ToDayCombo.Items.Add(new ComboBoxItem { Content = d.Name, Tag = d.Day });
            }

            ModeCombo.SelectedIndex = _initial.Mode switch
            {
                DynamicRangeMode.Fixed => 1,
                DynamicRangeMode.Weekday => 2,
                _ => 0
            };

            DaysBox.Text = _initial.Days.ToString(CultureInfo.InvariantCulture);
            FromDate.SelectedDate = _initial.FromDate ?? DateTime.Today.AddDays(-6);
            ToDate.SelectedDate = _initial.ToDate ?? DateTime.Today;

            SelectDay(FromDayCombo, _initial.FromDay);
            SelectDay(ToDayCombo, _initial.ToDay);

            FormatCombo.SelectedIndex = _initial.Format switch
            {
                DynamicDateFormat.RangeWithDash => 1,
                DynamicDateFormat.EndOnly => 2,
                DynamicDateFormat.EndLong => 3,
                _ => 0
            };

            UpdatePreview();
        }

        private static void SelectDay(System.Windows.Controls.ComboBox combo, DayOfWeek day)
        {
            foreach (var item in combo.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is DayOfWeek d && d == day)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        private static string TagOf(System.Windows.Controls.ComboBox combo)
            => (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;

        private static DayOfWeek DayOf(System.Windows.Controls.ComboBox combo)
            => (combo.SelectedItem as ComboBoxItem)?.Tag is DayOfWeek day ? day : DayOfWeek.Saturday;

        private void SimpleToken_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not string token || token.Length == 0) return;

            SelectedToken = "[" + token + "]";
            ResultingConfig = BuildConfig();
            DialogResult = true;
        }

        private void InsertRange_Click(object sender, RoutedEventArgs e)
        {
            SelectedToken = "[Rango]";
            ResultingConfig = BuildConfig();
            DialogResult = true;
        }

        private void Config_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

        private void UpdatePreview()
        {
            if (!IsInitialized) return;

            string mode = TagOf(ModeCombo);
            WeekPanel.Visibility = mode == "Week" ? Visibility.Visible : Visibility.Collapsed;
            FixedPanel.Visibility = mode == "Fixed" ? Visibility.Visible : Visibility.Collapsed;
            WeekdayPanel.Visibility = mode == "Weekday" ? Visibility.Visible : Visibility.Collapsed;

            try
            {
                var service = new DateTokenService();
                PreviewText.Text = service.Rango(BuildConfig());
            }
            catch (Exception ex)
            {
                PreviewText.Text = "Configuración no válida: " + ex.Message;
            }
        }

        private DynamicDateConfig BuildConfig()
        {
            var config = new DynamicDateConfig { Format = ParseFormat() };

            switch (TagOf(ModeCombo))
            {
                case "Fixed":
                    config.Mode = DynamicRangeMode.Fixed;
                    config.FromDate = FromDate.SelectedDate ?? DateTime.Today.AddDays(-6);
                    config.ToDate = ToDate.SelectedDate ?? DateTime.Today;
                    break;

                case "Weekday":
                    config.Mode = DynamicRangeMode.Weekday;
                    config.FromDay = DayOf(FromDayCombo);
                    config.ToDay = DayOf(ToDayCombo);
                    break;

                default:
                    config.Mode = DynamicRangeMode.Week;
                    config.Days = int.TryParse(DaysBox.Text, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int days)
                        ? days
                        : 7;
                    break;
            }

            return config;
        }

        private DynamicDateFormat ParseFormat() => TagOf(FormatCombo) switch
        {
            "RangeWithDash" => DynamicDateFormat.RangeWithDash,
            "EndOnly" => DynamicDateFormat.EndOnly,
            "EndLong" => DynamicDateFormat.EndLong,
            _ => DynamicDateFormat.RangeWithAl
        };

        /// <summary>
        /// Muestra el selector y devuelve el marcador elegido junto con la configuración
        /// resultante, o <c>null</c> si se canceló.
        /// </summary>
        public static (string Token, DynamicDateConfig? Config)? Pick(
            DynamicDateConfig? current, Window? owner)
        {
            var popup = new DateTokenPickerPopup(current) { Owner = owner };
            if (popup.ShowDialog() != true || popup.SelectedToken.Length == 0) return null;

            return (popup.SelectedToken, popup.ResultingConfig);
        }
    }
}

