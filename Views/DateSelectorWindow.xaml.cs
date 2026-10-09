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
        /// </summary>
        public bool AllowFixedMonthDay { get; }

        public DateSelectorWindow(string currentValue, bool allowFixedMonthDay = false)
        {
            InitializeComponent();
            AllowFixedMonthDay = allowFixedMonthDay;

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
