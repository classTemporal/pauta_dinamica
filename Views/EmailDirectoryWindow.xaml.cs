using System.Windows;
using System.Windows.Controls;

namespace PautaDinamicaApp.Views
{
    public partial class EmailDirectoryWindow : Window
    {
        public EmailDirectoryWindow()
        {
            InitializeComponent();
        }

        private void SourceFieldComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.SettingsViewModel vm && SourceFieldComboBox.SelectedItem is Models.FieldDefinition fd)
            {
                vm.SelectedPautaField = fd;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // FIX: "Aceptar y Regresar" cerraba sin persistir los correos escritos en el grid,
            // por lo que la validación de "Correos Faltantes" seguía marcándolos como inexistentes.
            if (DataContext is ViewModels.SettingsViewModel vm)
            {
                vm.SaveDirectoryChanges();
            }

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}