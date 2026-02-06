using System;
using System.Windows;
using System.Windows.Input; // Added for ICommand if needed later, though relay command is used.
using System.Linq; // Added for FirstOrDefault

using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PautaDinamicaApp.Views
{
    public partial class DateSelectorWindow : Window
    {
        public string SelectedValue { get; private set; } = "";

        public DateSelectorWindow(string currentValue)
        {
            InitializeComponent();
            if (currentValue == "TODAY")
            {
                MainCalendar.SelectedDate = DateTime.Today;
            }
            else if (DateTime.TryParse(currentValue, out DateTime date))
            {
                MainCalendar.SelectedDate = date;
            }
            else
            {
                MainCalendar.SelectedDate = DateTime.Today;
            }
        }

        private void MainCalendar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.Captured is CalendarItem)
            {
                Mouse.Capture(null);
            }

            // Buscar si se hizo clic en un día del calendario
            DependencyObject originalSource = (DependencyObject)e.OriginalSource;
            while ((originalSource != null) && !(originalSource is CalendarDayButton))
            {
                originalSource = VisualTreeHelper.GetParent(originalSource);
            }

            // Si el elemento clicado es un día, confirmamos la selección
            if (originalSource is CalendarDayButton)
            {
                OkButton_Click(sender, e);
            }
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedValue = "TODAY";
            DialogResult = true;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedValue = "";
            DialogResult = true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainCalendar.SelectedDate.HasValue)
            {
                SelectedValue = MainCalendar.SelectedDate.Value.ToString("dd/MM/yyyy");
            }
            DialogResult = true;
        }
    }
}
