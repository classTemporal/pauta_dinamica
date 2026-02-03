using System.Collections.Generic;
using System.Windows;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views
{
    public partial class TemplatePickerWindow : Window
    {
        public string SelectedTemplateContent { get; private set; } = string.Empty;

        public TemplatePickerWindow(List<MessageTemplate> templates)
        {
            InitializeComponent();
            DataContext = templates;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (TemplatesList.SelectedItem is MessageTemplate template)
            {
                SelectedTemplateContent = template.Content;
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Por favor, selecciona una plantilla de la lista.", "Selección Requerida");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
