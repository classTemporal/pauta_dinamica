using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views
{
    /// <summary>
    /// Selector de campos de la pauta activa para insertar en el cuerpo del correo.
    /// Devuelve el marcador en el formato que consume <c>EmailService</c>: <c>[Etiqueta]</c>.
    /// </summary>
    public partial class FieldPickerPopup : Window
    {
        private readonly List<FieldDefinition> _fields;
        private readonly ICollectionView _view;

        /// <summary>Marcador seleccionado, por ejemplo <c>[Nombre del sitio]</c>. Vacío si se canceló.</summary>
        public string SelectedPlaceholder { get; private set; } = string.Empty;

        public FieldPickerPopup(IEnumerable<FieldDefinition> fields)
        {
            InitializeComponent();

            _fields = (fields ?? Enumerable.Empty<FieldDefinition>())
                .Where(f => f != null && f.Type != FieldType.Separator)
                .OrderBy(f => f.Category, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(f => f.Order)
                .ToList();

            _view = CollectionViewSource.GetDefaultView(_fields);
            _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FieldDefinition.Category)));
            _view.Filter = FilterField;

            FieldList.ItemsSource = _view;

            if (_fields.Count == 0)
            {
                SearchBox.IsEnabled = false;
                SearchBox.Text = "Esta pauta no tiene campos definidos";
            }

            Loaded += (_, _) => SearchBox.Focus();
        }

        private bool FilterField(object item)
        {
            if (item is not FieldDefinition field) return false;

            string query = SearchBox?.Text?.Trim() ?? string.Empty;
            if (query.Length == 0) return true;

            return field.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || field.Id.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || (field.Category ?? string.Empty).Contains(query, StringComparison.CurrentCultureIgnoreCase);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => _view?.Refresh();

        private void FieldList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FieldList.SelectedItem is FieldDefinition) Confirm();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) => Confirm();

        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Confirm()
        {
            if (FieldList.SelectedItem is not FieldDefinition field)
            {
                System.Windows.MessageBox.Show("Seleccione un campo de la lista.", "Agregar campo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedPlaceholder = "[" + field.Label + "]";
            DialogResult = true;
        }

        /// <summary>
        /// Muestra el selector y devuelve el marcador elegido, o <c>null</c> si se canceló.
        /// </summary>
        public static string? Pick(IEnumerable<FieldDefinition> fields, Window? owner)
        {
            var popup = new FieldPickerPopup(fields) { Owner = owner };
            return popup.ShowDialog() == true && popup.SelectedPlaceholder.Length > 0
                ? popup.SelectedPlaceholder
                : null;
        }
    }
}
