using System;
using System.Windows;

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
