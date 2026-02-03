using System.Windows;
using System.Windows.Controls;
using PautaDinamicaApp.ViewModels;
using Application = System.Windows.Application;

namespace PautaDinamicaApp.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            var vm = new LoginViewModel();
            this.DataContext = vm;

            vm.OnLoginSuccess += () =>
            {
                var mainWin = new MainWindow();
                System.Windows.Application.Current.MainWindow = mainWin;
                mainWin.Show();
                this.Close();
            };

            vm.RequestClearPasswords += () =>
            {
                MainUserPasswordBox.Password = "";
                NewPasswordBox.Password = "";
                AdminRestoreBox.Password = "";
                AdminDeleteBox.Password = "";
                ResetNewPasswordBox.Password = "";
            };

            // Listen to password changes for all boxes
            MainUserPasswordBox.PasswordChanged += (s, e) => vm.Password = MainUserPasswordBox.Password;
            NewPasswordBox.PasswordChanged += (s, e) => vm.NewPassword = NewPasswordBox.Password;
            AdminRestoreBox.PasswordChanged += (s, e) => vm.AdminPassword = AdminRestoreBox.Password;
            AdminDeleteBox.PasswordChanged += (s, e) => vm.AdminPassword = AdminDeleteBox.Password;
            ResetNewPasswordBox.PasswordChanged += (s, e) => vm.ResetNewPassword = ResetNewPasswordBox.Password;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Allow dragging borderless window
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }
}
