using System;
using System.Windows;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views
{
    public partial class DateSelectorWindow : Window
    {
        public string SelectedValue { get; private set; } = "";

        /// <summary>
        /// Indica si se muestra la opción "día fijo para todos los meses".
        /// Solo debe ser <c>true</c> en configuración de pauta (valor predeterminado);
        /// en captura de datos, filtros o rangos se oculta para no guardar tokens dinámicos.
        /// El botón "fecha de hoy (dinámico)" también es exclusivo de configuración.
        /// </summary>
        public bool AllowFixedMonthDay { get; }

        /// <summary>
        /// En captura (dashboard) un solo clic en un día lo selecciona y cierra,
        /// para priorizar rapidez. En configuración se exige ACEPTAR.
        /// </summary>
        public bool SelectOnDayClick { get; }

        private bool _ready;

        public DateSelectorWindow(string currentValue, bool allowFixedMonthDay = false, bool selectOnDayClick = false)
        {
            InitializeComponent();
            AllowFixedMonthDay = allowFixedMonthDay;
            SelectOnDayClick = selectOnDayClick;

            if (AllowFixedMonthDay)
            {
                Title = "Seleccionar Fecha Predeterminada";
                FixedMonthDayCheck.Visibility = Visibility.Visible;
                MonthDayHint.Visibility = Visibility.Visible;
            }
            else
            {
                Title = "Seleccionar fecha";
                FixedMonthDayCheck.Visibility = Visibility.Collapsed;
                MonthDayHint.Visibility = Visibility.Collapsed;
                FixedMonthDayCheck.IsChecked = false;
            }

            if (string.Equals(currentValue?.Trim(), DateDefaultValue.TodayToken, StringComparison.OrdinalIgnoreCase))
            {
                MainCalendar.SelectedDate = DateTime.Today;
                FixedMonthDayCheck.IsChecked = false;
            }
            else if (DateDefaultValue.TryParseMonthDay(currentValue, out int monthDay))
            {
                if (AllowFixedMonthDay)
                {
                    FixedMonthDayCheck.IsChecked = true;
                    MainCalendar.SelectedDate = DateDefaultValue.ResolveMonthDay(monthDay);
                }
                else
                {
                    // Fuera de configuración no se usan tokens: se muestra su fecha resuelta.
                    MainCalendar.SelectedDate = DateDefaultValue.ResolveMonthDay(monthDay);
                }
            }
            else if (DateTime.TryParse(currentValue, out DateTime date))
            {
                MainCalendar.SelectedDate = date;
                FixedMonthDayCheck.IsChecked = false;
            }
            else
            {
                MainCalendar.SelectedDate = DateTime.Today;
                FixedMonthDayCheck.IsChecked = false;
            }

            UpdateMonthDayHint();

            // El cambio programático de SelectedDate arriba dispara SelectedDatesChanged;
            // solo los clics posteriores del usuario auto-aceptan (modo dashboard).
            Loaded += (_, _) => _ready = true;
        }

        private int SelectedDay => MainCalendar.SelectedDate?.Day ?? DateTime.Today.Day;

        private void UpdateMonthDayHint()
        {
            if (!AllowFixedMonthDay) return;

            if (FixedMonthDayCheck.IsChecked == true)
            {
                int day = SelectedDay;
                DateTime resolved = DateDefaultValue.ResolveMonthDay(day, MainCalendar.SelectedDate ?? DateTime.Today);
                MonthDayHint.Text = $"Día {day} de cada mes (este mes sería {resolved:dd/MM/yyyy}). Si un mes no tiene día {day}, se usa el último día.";
            }
            else
            {
                MonthDayHint.Text = "Elige un día en el calendario y marca la casilla para repetirlo cada mes sin cambiarlo a mano.";
            }
        }

        private void MainCalendar_SelectedDatesChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateMonthDayHint();

            if (SelectOnDayClick && _ready && MainCalendar.SelectedDate.HasValue)
                AcceptDate(MainCalendar.SelectedDate.Value);
        }

        /// <summary>
        /// Click directo sobre un día (modo dashboard): cierra aunque el día ya estuviera
        /// seleccionado (en ese caso no hay SelectedDatesChanged que lo detecte).
        /// Los clics en encabezado/flechas de mes se ignoran.
        /// </summary>
        private void MainCalendar_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!SelectOnDayClick || !_ready) return;
            System.Windows.DependencyObject? obj = e.OriginalSource as System.Windows.DependencyObject;
            while (obj != null && !(obj is System.Windows.Controls.Primitives.CalendarDayButton))
                obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
            if (obj is System.Windows.Controls.Primitives.CalendarDayButton day
                && day.DataContext is DateTime date)
                AcceptDate(date);
        }

        private bool _accepted;

        private void AcceptDate(DateTime date)
        {
            if (_accepted) return;
            _accepted = true;
            SelectedValue = date.ToString("dd/MM/yyyy");
            DialogResult = true;
        }

        private void FixedMonthDayCheck_Changed(object sender, RoutedEventArgs e)
        {
            UpdateMonthDayHint();
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            // En configuración se guarda el token dinámico; fuera de ella los
            // llamadores lo resuelven a la fecha de hoy al recibirlo.
            SelectedValue = AllowFixedMonthDay
                ? DateDefaultValue.TodayToken
                : DateTime.Today.ToString("dd/MM/yyyy");
            DialogResult = true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainCalendar.SelectedDate.HasValue)
            {
                if (AllowFixedMonthDay && FixedMonthDayCheck.IsChecked == true)
                    SelectedValue = $"{DateDefaultValue.MonthDayPrefix}{MainCalendar.SelectedDate.Value.Day}";
                else
                    SelectedValue = MainCalendar.SelectedDate.Value.ToString("dd/MM/yyyy");
            }
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
