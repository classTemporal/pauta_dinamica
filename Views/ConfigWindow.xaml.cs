using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp
{
    public partial class ConfigWindow : Window
    {
        private Point _startPoint;

        public ConfigWindow(string activePautaId = "")
        {
            InitializeComponent();
            this.DataContext = new ViewModels.EditorViewModel(activePautaId);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ViewModels.EditorViewModel vm)
            {
                if (vm.SaveConfigCommand.CanExecute(null))
                {
                    vm.SaveConfigCommand.Execute(null);
                }

                if (vm.IsSaveSuccessful)
                {
                    this.DialogResult = true;
                    this.Close();
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (this.DialogResult != true && this.DataContext is ViewModels.EditorViewModel vm)
            {
                if (vm.HasPendingChanges())
                {
                    var result = MessageBox.Show(
                        "Se han detectado cambios sin guardar. Si sale ahora, perderá todos los cambios realizados.\r\n\r\n¿Desea salir de todos modos?",
                        "Cambios sin guardar",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        e.Cancel = true;
                    }
                }
            }
            base.OnClosing(e);
        }

        // --- Lógica de Drag & Drop para Reordenar Filas ---

        private void EditorGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void EditorGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DataGrid dataGrid = (DataGrid)sender;
                    DependencyObject? originalSource = e.OriginalSource as DependencyObject;
                    DataGridRow? row = originalSource != null ? FindVisualParent<DataGridRow>(originalSource) : null;

                    if (row != null)
                    {
                        FieldDefinition field = (FieldDefinition)row.Item;
                        DataObject dragData = new DataObject("FieldDefinition", field);
                        DragDrop.DoDragDrop(row, dragData, DragDropEffects.Move);
                    }
                }
            }
        }

        private void EditorGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("FieldDefinition"))
            {
                FieldDefinition? droppedField = e.Data.GetData("FieldDefinition") as FieldDefinition;
                DataGrid dataGrid = (DataGrid)sender;
                DependencyObject? originalSource = e.OriginalSource as DependencyObject;
                DataGridRow? row = originalSource != null ? FindVisualParent<DataGridRow>(originalSource) : null;

                if (droppedField != null && dataGrid.DataContext is ViewModels.EditorViewModel vm)
                {
                    int oldIndex = vm.Fields.IndexOf(droppedField);
                    int newIndex = -1;

                    if (row != null)
                    {
                        newIndex = vm.Fields.IndexOf((FieldDefinition)row.Item);
                    }
                    else
                    {
                        // Si se suelta al final de la lista
                        newIndex = vm.Fields.Count - 1;
                    }

                    if (newIndex != -1 && oldIndex != newIndex)
                    {
                        vm.Fields.Move(oldIndex, newIndex);
                    }
                }
            }
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T? parent = parentObject as T;
            if (parent != null) return parent;
            return FindVisualParent<T>(parentObject);
        }
    }
}
