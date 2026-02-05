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
    }
}
