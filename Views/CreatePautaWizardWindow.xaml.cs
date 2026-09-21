using System.Windows;

namespace PautaDinamicaApp.Views
{
    public partial class CreatePautaWizardWindow : Window
    {
        public CreatePautaWizardWindow()
        {
            InitializeComponent();
            var vm = new ViewModels.CreatePautaWizardViewModel();
            this.DataContext = vm;

            // Refresh Commands after PautaName changes
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModels.CreatePautaWizardViewModel.PautaName))
                {
                    vm.Steps[0].IsCompleted = !string.IsNullOrWhiteSpace(vm.PautaName);
                }
            };
        }
    }
}
