using System.Collections.Generic;
using System.Windows;
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
                System.Windows.MessageBox.Show("Por favor seleccione una configuración.");
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
