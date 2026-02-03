using System.Windows;

namespace PautaDinamicaApp.Views
{
    public partial class TemplateManagementWindow : Window
    {
        public TemplateManagementWindow()
        {
            InitializeComponent();
            DataContextChanged += (s, e) =>
            {
                if (DataContext is ViewModels.TemplateManagementViewModel vm)
                {
                    vm.RequestClose += () => Close();
                }
            };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
