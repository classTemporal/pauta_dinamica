using System;
using System.Linq;
using System.Windows;

namespace PautaDinamicaApp.Views
{
    public partial class TimeSelectorWindow : Window
    {
        public string SelectedValue { get; private set; } = "";
        private string _timeFormat = "HH:mm";
        private bool _isAmPm => _timeFormat.Contains("tt");

        public TimeSelectorWindow(string currentValue, string format = "HH:mm")
        {
            InitializeComponent();
            _timeFormat = format;

            bool isAmPm = _isAmPm;
            AmPmCombo.Visibility = isAmPm ? Visibility.Visible : Visibility.Collapsed;

            HourCombo.Items.Clear();
            int maxHour = isAmPm ? 12 : 23;
            int minHour = isAmPm ? 1 : 0;

            for (int i = minHour; i <= maxHour; i++) HourCombo.Items.Add(i.ToString("D2"));
            for (int i = 0; i < 60; i++) MinuteCombo.Items.Add(i.ToString("D2"));

            if (currentValue == "NOW")
            {
                SetCurrentTime();
            }
            else if (DateTime.TryParse(currentValue, out DateTime time))
            {
                if (isAmPm)
                {
                    int h = time.Hour;
                    string tt = h >= 12 ? "PM" : "AM";
                    if (h > 12) h -= 12;
                    if (h == 0) h = 12;

                    HourCombo.SelectedItem = h.ToString("D2");
                    AmPmCombo.SelectedIndex = tt == "AM" ? 0 : 1;
                }
                else
                {
                    HourCombo.SelectedItem = time.Hour.ToString("D2");
                }
                MinuteCombo.SelectedItem = time.Minute.ToString("D2");
            }
            else
            {
                SetCurrentTime();
            }
        }

        private void SetCurrentTime()
        {
            var now = DateTime.Now;
            if (_isAmPm)
            {
                int h = now.Hour;
                string tt = h >= 12 ? "PM" : "AM";
                if (h > 12) h -= 12;
                if (h == 0) h = 12;

                HourCombo.SelectedItem = h.ToString("D2");
                AmPmCombo.SelectedIndex = tt == "AM" ? 0 : 1;
            }
            else
            {
                HourCombo.SelectedItem = now.Hour.ToString("D2");
            }
            MinuteCombo.SelectedItem = now.Minute.ToString("D2");
        }

        private void NowButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedValue = "NOW";
            DialogResult = true;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedValue = "";
            DialogResult = true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(HourCombo.SelectedItem?.ToString(), out int h) &&
                int.TryParse(MinuteCombo.SelectedItem?.ToString(), out int m))
            {
                if (_isAmPm && AmPmCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Content is string tt)
                {
                    if (tt == "PM" && h < 12) h += 12;
                    if (tt == "AM" && h == 12) h = 0;
                }

                var dt = new DateTime(2000, 1, 1, h, m, 0);
                SelectedValue = dt.ToString(_timeFormat);
                DialogResult = true;
            }
        }
    }
}
