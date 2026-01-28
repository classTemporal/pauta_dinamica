using System.Windows;

namespace PautaDinamicaApp
{
    public partial class ConfigWindow : Window
    {
        public ConfigWindow()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ViewModels.EditorViewModel vm)
            {
                if (vm.IsSaveSuccessful)
                {
                    this.DialogResult = true;
                    this.Close();
                }
            }
        }
    }
}
