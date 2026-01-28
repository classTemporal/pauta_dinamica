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
                if (vm.IsSaveSuccessful)
                {
                    this.DialogResult = true;
                    this.Close();
                }
            }
        }
    }
}
