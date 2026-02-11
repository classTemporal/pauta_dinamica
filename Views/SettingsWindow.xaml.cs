using System;
using System.Windows;

namespace PautaDinamicaApp.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            AdminPassBox.PasswordChanged += (s, e) =>
            {
                if (DataContext is ViewModels.SettingsViewModel vm)
                {
                    vm.AdminPassword = AdminPassBox.Password;
                }
            };
        }

        private void EmailBody_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (EmailBodyTextBox != null)
            {
                double newHeight = EmailBodyTextBox.Height + e.VerticalChange;
                if (newHeight >= EmailBodyTextBox.MinHeight)
                {
                    EmailBodyTextBox.Height = newHeight;
                }
            }
        }
    }
}
