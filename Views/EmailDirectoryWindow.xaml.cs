using System.Windows;

namespace PautaDinamicaApp.Views
{
    public partial class EmailDirectoryWindow : Window
    {
        public EmailDirectoryWindow()
        {
            InitializeComponent();
        }

        private void SourceFieldComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // In WPF, ComboBox.SelectedValue with UpdateSourceTrigger=PropertyChanged does not
            // reliably push the updated value to the binding source on selection change.
            // Force the binding to update the source immediately so that
            // SelectedPauta.EmailNameFieldId is set, which triggers the PropertyChanged chain
            // -> OnSelectedPautaPropertyChanged -> AutoDetectContactsFromRecords().
            if (sender is System.Windows.Controls.ComboBox comboBox)
            {
                comboBox.GetBindingExpression(System.Windows.Controls.ComboBox.SelectedValueProperty)?.UpdateSource();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
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