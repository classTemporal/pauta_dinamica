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
        private DateTime _fromDate;
        private DateTime _toDate;

        private string? _selectedSimple;

        public DateTokenPickerPopup(DynamicDateConfig? current = null)
        {
            InitializeComponent();

            _initial = current ?? new DynamicDateConfig();

            // Domingo y sábado primero, luego el resto de la semana.
            var dayNames = new (DayOfWeek Day, string Name)[]
            {
                (DayOfWeek.Sunday, "Domingo"),
                (DayOfWeek.Saturday, "Sábado"),
                (DayOfWeek.Monday, "Lunes"),
                (DayOfWeek.Tuesday, "Martes"),
                (DayOfWeek.Wednesday, "Miércoles"),
                (DayOfWeek.Thursday, "Jueves"),
                (DayOfWeek.Friday, "Viernes"),
            };

            foreach (var d in dayNames)
            {
                FromDayCombo.Items.Add(new ComboBoxItem { Content = d.Name, Tag = d.Day });
            }

            // Día inicial de [Semana]: de lunes a domingo.
            foreach (var d in new (DayOfWeek Day, string Name)[]
            {
                (DayOfWeek.Monday, "Lunes"),
                (DayOfWeek.Tuesday, "Martes"),
                (DayOfWeek.Wednesday, "Miércoles"),
                (DayOfWeek.Thursday, "Jueves"),
                (DayOfWeek.Friday, "Viernes"),
                (DayOfWeek.Saturday, "Sábado"),
                (DayOfWeek.Sunday, "Domingo"),
            })
            {
                SemStartCombo.Items.Add(new ComboBoxItem { Content = d.Name, Tag = d.Day });
            }
            SemStartCombo.SelectedIndex = 0;

            ModeCombo.SelectedIndex = _initial.Mode switch
            {
                DynamicRangeMode.Week => 1,
                DynamicRangeMode.Fixed => 2,
                _ => 0
            };

            DaysBox.Text = _initial.Days.ToString(CultureInfo.InvariantCulture);
            _fromDate = _initial.FromDate ?? DateTime.Today.AddDays(-6);
            _toDate = _initial.ToDate ?? DateTime.Today;
            FromDateBox.Text = _fromDate.ToString("dd/MM/yyyy");
            ToDateBox.Text = _toDate.ToString("dd/MM/yyyy");

            SelectDay(FromDayCombo, _initial.FromDay);

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

            // Solo selecciona; la inserción la hace el botón Insertar.
            // [Semana] despliega su día inicial.
            _selectedSimple = token;
            RangeBorder.Visibility = Visibility.Collapsed;
            RangeToggleBtn.Content = "⚙ Rango personalizado...";
            SemanaPanel.Visibility = token == "Semana" ? Visibility.Visible : Visibility.Collapsed;
            RefreshTokenSelection();
        }

        private void ToggleRange_Click(object sender, RoutedEventArgs e)
        {
            bool show = RangeBorder.Visibility != Visibility.Visible;
            RangeBorder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            RangeToggleBtn.Content = show ? "⚙ Rango personalizado (ocultar)" : "⚙ Rango personalizado...";
            if (show)
            {
                _selectedSimple = null;
                RefreshTokenSelection();
                SemanaPanel.Visibility = Visibility.Collapsed;
                UpdatePreview();
            }
            InsertBtn.Content = "Insertar";
        }

        private void InsertBtn_Click(object sender, RoutedEventArgs e)
        {
            // Rango abierto: inserta el token del modo actual (autodetectado),
            // autocontenido para poder usar varios distintos en el mismo correo.
            if (RangeBorder.Visibility == Visibility.Visible)
            {
                SelectedToken = BuildParamToken();
                ResultingConfig = null;
                DialogResult = true;
                return;
            }

            if (string.IsNullOrEmpty(_selectedSimple))
            {
                System.Windows.MessageBox.Show("Seleccione una opción de fecha o abra el rango personalizado.",
                    "Insertar fecha", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // [Semana] con día inicial: token autocontenido [Semana:Lun].
            if (_selectedSimple == "Semana")
            {
                var start = (SemStartCombo.SelectedItem as ComboBoxItem)?.Tag is DayOfWeek d ? d : DayOfWeek.Monday;
                SelectedToken = $"[Semana:{DateTokenService.DayAbbrev(start)}]";
                ResultingConfig = null;
                DialogResult = true;
                return;
            }

            SelectedToken = "[" + _selectedSimple + "]";
            ResultingConfig = BuildConfig();
            DialogResult = true;
        }

        private string BuildParamToken()
        {
            switch (TagOf(ModeCombo))
            {
                case "Fixed": return $"[Rango:{_fromDate:yyyy-MM-dd}_{_toDate:yyyy-MM-dd}]";

                case "Weekday":
                    return $"[Rango:{DateTokenService.DayAbbrev(DayOf(FromDayCombo))}]";

                default:
                    int days = int.TryParse(DaysBox.Text, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int d) ? d : 7;
                    return $"[Rango:{days}d]";
            }
        }

        private void PickFromDate_Click(object sender, RoutedEventArgs e)
        {
            if (PickCalendarDate(FromDateBox.Text) is DateTime d)
            {
                _fromDate = d;
                FromDateBox.Text = d.ToString("dd/MM/yyyy");
                UpdatePreview();
            }
        }

        private void PickToDate_Click(object sender, RoutedEventArgs e)
        {
            if (PickCalendarDate(ToDateBox.Text) is DateTime d)
            {
                _toDate = d;
                ToDateBox.Text = d.ToString("dd/MM/yyyy");
                UpdatePreview();
            }
        }

        private DateTime? PickCalendarDate(string current)
        {
            var win = new DateSelectorWindow(current) { Owner = this };
            if (win.ShowDialog() != true) return null;
            if (win.SelectedValue.Trim().Equals(Models.DateDefaultValue.TodayToken, StringComparison.OrdinalIgnoreCase)) return DateTime.Today;
            if (Models.DateDefaultValue.TryParseMonthDay(win.SelectedValue, out int day))
                return Models.DateDefaultValue.ResolveMonthDay(day);
            if (DateTime.TryParse(win.SelectedValue, out DateTime d)) return d.Date;
            return null;
        }

        private void RefreshTokenSelection()
        {
            var accent = TryFindResource("AccentBrush") as System.Windows.Media.Brush;            var normal = TryFindResource("BorderBrush") as System.Windows.Media.Brush;
            foreach (var btn in new[] { HoyBtn, SemanaBtn, MesBtn, AnoBtn })
            {
                bool selected = (btn.Tag as string) == _selectedSimple;
                btn.BorderBrush = selected ? accent : normal;
                btn.BorderThickness = new Thickness(selected ? 2 : 1);
            }
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
                RangeTitle.Text = "Rango personalizado " + BuildParamToken();
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
                    config.FromDate = _fromDate;
                    config.ToDate = _toDate;
                    break;

                case "Weekday":
                    config.Mode = DynamicRangeMode.Weekday;
                    config.FromDay = DayOf(FromDayCombo);
                    // Una semana desde el día elegido.
                    config.ToDay = (DayOfWeek)(((int)config.FromDay + 6) % 7);
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

