using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PautaDinamicaApp.ViewModels;

namespace PautaDinamicaApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.FieldsRefreshed += RebuildColumns;
                    RebuildColumns();
                }
            };
        }

        private void RebuildColumns()
        {
            if (DataContext is not MainViewModel vm) return;
            if (RecordsGrid == null) return;

            // Limpiar todas las columnas excepto las primeras dos (Checkbox y Acciones)
            while (RecordsGrid.Columns.Count > 2)
            {
                RecordsGrid.Columns.RemoveAt(2);
            }

            // Crear columnas dinámicas basadas en la configuración actual
            foreach (var field in vm.CurrentFields)
            {
                var column = new DataGridTextColumn
                {
                    Header = field.Label,
                    Binding = new System.Windows.Data.Binding($"Values")
                    {
                        Converter = (IValueConverter)System.Windows.Application.Current.Resources["DictionaryValueConverter"],
                        ConverterParameter = field.Id
                    },
                    Width = DataGridLength.Auto,
                    MinWidth = 180 // Suficiente espacio para que no se vea apretado
                };

                RecordsGrid.Columns.Add(column);
            }
        }
    }
}