using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PautaDinamicaApp.Models;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using DataObject = System.Windows.DataObject;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.Linq;
using System.Runtime.InteropServices;


namespace PautaDinamicaApp
{
    public partial class ConfigWindow : Window
    {
        private System.Windows.Point _startPoint;
        private ListBoxItem? _draggedItem;
        private bool _isDraggingNow;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public ConfigWindow(string activePautaId = "")
        {
            InitializeComponent();
            var vm = new ViewModels.EditorViewModel(activePautaId);
            this.DataContext = vm;

            // Auto-scroll logic when items move
            vm.Fields.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
                    ScrollFirstSelected(EditorGrid);
            };
            vm.ExportColumns.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
                    ScrollFirstSelected(ExportGrid);
            };
            vm.PdfColumns.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
                    ScrollFirstSelected(PdfGrid);
            };
        }

        private void ScrollFirstSelected(DataGrid grid)
        {
            if (grid == null) return;
            var items = grid.ItemsSource as System.Collections.IEnumerable;
            if (items == null) return;

            object? firstSelected = null;
            foreach (var item in items)
            {
                if (item is FieldDefinition f && f.IsSelected) { firstSelected = item; break; }
                if (item is ExportColumnConfig c && c.IsSelected) { firstSelected = item; break; }
            }

            if (firstSelected != null)
            {
                grid.ScrollIntoView(firstSelected);
            }
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

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ViewModels.EditorViewModel vm)
            {
                if (vm.ApplyConfigCommand.CanExecute(null))
                {
                    vm.ApplyConfigCommand.Execute(null);
                }

                if (vm.IsSaveSuccessful)
                {
                    System.Windows.MessageBox.Show("Cambios aplicados correctamente.", "Éxito");
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
                    var result = System.Windows.MessageBox.Show(
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
                System.Windows.Point mousePos = e.GetPosition(null);
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

        // --- Drag & Drop para Pautas ---

        private void PautaList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _draggedItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            _isDraggingNow = false;
        }

        private void PautaList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Solo seleccionamos si NO hubo arrastre y no fue clic en botón/checkbox
            if (!_isDraggingNow && _draggedItem != null && this.DataContext is ViewModels.EditorViewModel vm)
            {
                var pauta = _draggedItem.DataContext as PautaSchema;
                if (pauta != null)
                {
                    vm.EditingPauta = pauta;
                    PautaList.SelectedItem = pauta; // Sincronizar visualmente si es necesario
                }
            }
            _draggedItem = null;
        }

        private void PautaList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDraggingNow = true;
                    var pauta = _draggedItem.DataContext as PautaSchema;

                    if (pauta != null && !pauta.IsRenaming)
                    {
                        DataObject dragData = new DataObject("PautaSchema", pauta);
                        pauta.IsDragging = true;

                        // Crear un visual para el arrastre
                        var dragWindow = CreateDragVisual(_draggedItem);
                        dragWindow.Show();

                        // Suscribirse al evento para actualizar la posición
                        System.Windows.GiveFeedbackEventHandler feedbackHandler = (s, args) =>
                        {
                            UpdateDragVisualPosition(dragWindow);
                        };

                        _draggedItem.GiveFeedback += feedbackHandler;
                        
                        try {
                            DragDrop.DoDragDrop(_draggedItem, dragData, DragDropEffects.Move);
                        }
                        finally {
                            _draggedItem.GiveFeedback -= feedbackHandler;
                            pauta.IsDragging = false;
                            dragWindow.Close();
                            _isDraggingNow = false;
                        }
                    }
                }
            }
        }

        private void PautaList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PautaSchema"))
            {
                PautaSchema? droppedPauta = e.Data.GetData("PautaSchema") as PautaSchema;
                DependencyObject? originalSource = e.OriginalSource as DependencyObject;
                ListBoxItem? item = originalSource != null ? FindVisualParent<ListBoxItem>(originalSource) : null;

                if (droppedPauta != null && this.DataContext is ViewModels.EditorViewModel vm)
                {
                    int oldIndex = vm.Pautas.IndexOf(droppedPauta);
                    int newIndex = -1;

                    if (item != null)
                    {
                        newIndex = vm.Pautas.IndexOf((PautaSchema)item.DataContext);
                    }
                    else
                    {
                        newIndex = vm.Pautas.Count - 1;
                    }

                    if (newIndex != -1 && oldIndex != newIndex)
                    {
                        vm.Pautas.Move(oldIndex, newIndex);
                        vm.MarkDatabaseModified();
                    }

                    // Limpiar estados de drop target
                    foreach (var p in vm.Pautas) p.IsDropTarget = false;
                }
            }
        }

        private void PautaItem_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PautaSchema") && sender is ListBoxItem item && item.DataContext is PautaSchema pauta)
            {
                pauta.IsDropTarget = true;
            }
        }

        private void PautaItem_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is PautaSchema pauta)
            {
                pauta.IsDropTarget = false;
            }
        }

        // --- Edición de Nombre de Pauta ---

        private void PautaNameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FinalizeRename(sender);
            }
            else if (e.Key == Key.Escape)
            {
                CancelRename(sender);
            }
        }

        private void PautaNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            FinalizeRename(sender);
        }

        private void FinalizeRename(object sender)
        {
            if (sender is System.Windows.Controls.TextBox tb && tb.DataContext is PautaSchema pauta)
            {
                pauta.IsRenaming = false;
                if (this.DataContext is ViewModels.EditorViewModel vm)
                {
                    vm.MarkDatabaseModified();
                }
            }
        }

        private void CancelRename(object sender)
        {
            if (sender is System.Windows.Controls.TextBox tb && tb.DataContext is PautaSchema pauta)
            {
                pauta.IsRenaming = false;
            }
        }

        private void RenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb && (bool)e.NewValue)
            {
                tb.Focus();
                tb.SelectAll();
            }
        }

        // --- Ayudantes Visuales para Arrastre ---

        private Window CreateDragVisual(FrameworkElement source)
        {
            var visual = new Border
            {
                Background = this.TryFindResource("CardBackgroundBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White,
                BorderBrush = this.TryFindResource("AccentBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Blue,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Opacity = 0.7,
                Child = new TextBlock
                {
                    Text = (source.DataContext as PautaSchema)?.Name ?? "Arrastrando...",
                    FontWeight = FontWeights.Bold,
                    Foreground = this.TryFindResource("TextBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Black
                }
            };

            var window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                ShowInTaskbar = false,
                IsHitTestVisible = false,
                Content = visual
            };

            UpdateDragVisualPosition(window);
            return window;
        }

        private void UpdateDragVisualPosition(Window window)
        {
            if (GetCursorPos(out POINT lpPoint))
            {
                window.Left = lpPoint.X + 15;
                window.Top = lpPoint.Y + 15;
            }
        }

        private System.Windows.Point GetMousePosition()
        {
            return PointToScreen(Mouse.GetPosition(this));
        }

        private void ClearPdfField1_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ViewModels.EditorViewModel vm && vm.EditingPauta != null)
            {
                vm.EditingPauta.PdfFileNameFieldId1 = "";
            }
        }

        private void ClearPdfField2_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ViewModels.EditorViewModel vm && vm.EditingPauta != null)
            {
                vm.EditingPauta.PdfFileNameFieldId2 = "";
            }
        }
    }
}
