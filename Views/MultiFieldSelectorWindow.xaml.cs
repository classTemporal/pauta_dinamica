using System.Collections.Generic;
using System.Windows;
using PautaDinamicaApp.ViewModels;

namespace PautaDinamicaApp.Views
{
    public partial class MultiFieldSelectorWindow : Window
    {
        public MultiFieldSelectorWindow(IEnumerable<SelectableFieldViewModel> fields)
        {
            InitializeComponent();
            DataContext = fields;
            FieldsList.ItemsSource = fields;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
