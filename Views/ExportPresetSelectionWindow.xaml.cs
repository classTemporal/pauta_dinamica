using System.Collections.Generic;
using System.Windows;
using PautaDinamicaApp;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views
{
    public partial class ExportPresetSelectionWindow : Window
    {
        public ExportPreset? SelectedPreset { get; private set; }

        public ExportPresetSelectionWindow(List<ExportPreset> presets)
        {
            InitializeComponent();
            PresetsList.ItemsSource = presets;
            if (presets.Count > 0) PresetsList.SelectedIndex = 0;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            SelectedPreset = PresetsList.SelectedItem as ExportPreset;
            if (SelectedPreset == null)
            {
                MessageBoxHelper.Show("Por favor seleccione una configuración.", "Selección Requerida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
