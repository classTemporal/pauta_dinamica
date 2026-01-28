using System.Windows;

namespace PautaDinamicaApp
{
    public partial class ConfigWindow : Window
    {
        public ConfigWindow(bool hasRecords = false)
        {
            InitializeComponent();
            this.DataContext = new ViewModels.EditorViewModel(hasRecords);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ViewModels.EditorViewModel vm)
            {
                // Ejecutamos manualmente el comando para asegurar el orden.
                // Al haber quitado el Command del XAML, ahora controlamos el flujo exacto aquí.
                if (vm.SaveConfigCommand.CanExecute(null))
                {
                    vm.SaveConfigCommand.Execute(null);
                }

                if (vm.IsSaveSuccessful)
                {
                    this.DialogResult = true;
                    this.Close();
                }
            }
        }
    }
}
