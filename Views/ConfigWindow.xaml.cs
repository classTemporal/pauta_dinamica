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

        private void ScrollFirstSelected(System.Windows.Controls.ListBox grid)
        {
            if (grid == null) return;
            var items = grid.ItemsSource as System.Collections.IEnumerable;
            if (items == null) return;

            object? firstSelected = null;
            foreach (var item in items)
            {
                if (item is FieldDefinition f && f.IsSelected) { firstSelected = item; break; }
                if (item is ExportColumnConfig ec && ec.IsSelected) { firstSelected = item; break; }
                if (item is PdfReplacementRule pr && pr.IsSelected) { firstSelected = item; break; }
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
            _draggedItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            _isDraggingNow = false;

            if (_draggedItem != null)
            {
                var source = e.OriginalSource as DependencyObject;
                if (IsFocusableControl(source)) return;
                
                // Marcamos Handled = true para evitar selección inmediata en MouseDown
                e.Handled = true;
            }
        }

        private void EditorGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingNow && _draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                if (EditorGrid.SelectedItem != _draggedItem.DataContext)
                {
                    EditorGrid.SelectedItem = _draggedItem.DataContext;
                }
            }
            _draggedItem = null;
        }

        private void EditorGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDraggingNow = true;
                    FieldDefinition field = (FieldDefinition)_draggedItem.DataContext;
                    EditorGrid.SelectedItem = field;
                    DataObject dragData = new DataObject("FieldDefinition", field);

                    var dragWindow = CreateDragVisual(_draggedItem, field.Label);
                    dragWindow.Show();

                    System.Windows.GiveFeedbackEventHandler feedbackHandler = (s, args) => UpdateDragVisualPosition(dragWindow);
                    _draggedItem.GiveFeedback += feedbackHandler;

                    try {
                        DragDrop.DoDragDrop(_draggedItem, dragData, DragDropEffects.Move);
                    }
                    finally {
                        _draggedItem.GiveFeedback -= feedbackHandler;
                        dragWindow.Close();
                        _isDraggingNow = false;
                    }
                }
            }
        }

        private void EditorGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("FieldDefinition"))
            {
                FieldDefinition? droppedField = e.Data.GetData("FieldDefinition") as FieldDefinition;
                System.Windows.Controls.ListBox listBox = (System.Windows.Controls.ListBox)sender;
                DependencyObject? originalSource = e.OriginalSource as DependencyObject;
                ListBoxItem? item = FindVisualParent<ListBoxItem>(originalSource);

                if (droppedField != null && listBox.DataContext is ViewModels.EditorViewModel vm)
                {
                    int oldIndex = vm.Fields.IndexOf(droppedField);
                    int newIndex = -1;

                    if (item != null && item.DataContext is FieldDefinition targetField)
                    {
                        newIndex = vm.Fields.IndexOf(targetField);
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

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
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

            if (_draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
            }
        }

        private void PautaList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Solo seleccionamos si NO hubo arrastre y no fue clic en botón/checkbox/textbox
            if (!_isDraggingNow && _draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject) && this.DataContext is ViewModels.EditorViewModel vm)
            {
                var pauta = _draggedItem.DataContext as PautaSchema;
                if (pauta != null)
                {
                    vm.EditingPauta = pauta;
                    PautaList.SelectedItem = pauta;
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
                        PautaList.SelectedItem = pauta;
                        DataObject dragData = new DataObject("PautaSchema", pauta);
                        pauta.IsDragging = true;

                        var dragWindow = CreateDragVisual(_draggedItem, pauta.Name);
                        dragWindow.Show();

                        System.Windows.GiveFeedbackEventHandler feedbackHandler = (s, args) => UpdateDragVisualPosition(dragWindow);
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

        private bool IsFocusableControl(DependencyObject? obj)
        {
            if (obj == null) return false;
            var parent = obj;
            while (parent != null && !(parent is ListBoxItem))
            {
                if (parent is System.Windows.Controls.Button || parent is System.Windows.Controls.CheckBox || parent is System.Windows.Controls.TextBox || parent is System.Windows.Controls.ComboBox || parent is System.Windows.Controls.Primitives.Thumb)
                    return true;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return false;
        }

        // --- Drag & Drop para Excel Export ---

        private void ExportGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _draggedItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            _isDraggingNow = false;

            if (_draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
            }
        }

        private void ExportGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingNow && _draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                ExportGrid.SelectedItem = _draggedItem.DataContext;
            }
            _draggedItem = null;
        }

        private void ExportGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDraggingNow = true;
                    var column = _draggedItem.DataContext as ExportColumnConfig;
                    if (column != null)
                    {
                        ExportGrid.SelectedItem = column;
                        DataObject dragData = new DataObject("ExportColumnConfig", column);
                        var dragWindow = CreateDragVisual(_draggedItem, column.CustomHeader ?? column.OriginalLabel);
                        dragWindow.Show();

                        System.Windows.GiveFeedbackEventHandler feedbackHandler = (s, args) => UpdateDragVisualPosition(dragWindow);
                        _draggedItem.GiveFeedback += feedbackHandler;

                        try { DragDrop.DoDragDrop(_draggedItem, dragData, DragDropEffects.Move); }
                        finally { _draggedItem.GiveFeedback -= feedbackHandler; dragWindow.Close(); _isDraggingNow = false; }
                    }
                }
            }
        }

        private void ExportGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ExportColumnConfig"))
            {
                var dropped = e.Data.GetData("ExportColumnConfig") as ExportColumnConfig;
                var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
                if (dropped != null && this.DataContext is ViewModels.EditorViewModel vm)
                {
                    int oldIdx = vm.ExportColumns.IndexOf(dropped);
                    int newIdx = item != null ? vm.ExportColumns.IndexOf((ExportColumnConfig)item.DataContext) : vm.ExportColumns.Count - 1;
                    if (newIdx != -1 && oldIdx != newIdx) vm.ExportColumns.Move(oldIdx, newIdx);
                }
            }
        }

        // --- Drag & Drop para PDF Export ---

        private void PdfGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _draggedItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            _isDraggingNow = false;

            if (_draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
            }
        }

        private void PdfGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingNow && _draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                PdfGrid.SelectedItem = _draggedItem.DataContext;
            }
            _draggedItem = null;
        }

        private void PdfGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDraggingNow = true;
                    var column = _draggedItem.DataContext as ExportColumnConfig;
                    if (column != null)
                    {
                        PdfGrid.SelectedItem = column;
                        DataObject dragData = new DataObject("PdfColumnConfig", column);
                        var dragWindow = CreateDragVisual(_draggedItem, column.CustomHeader ?? column.OriginalLabel);
                        dragWindow.Show();

                        System.Windows.GiveFeedbackEventHandler feedbackHandler = (s, args) => UpdateDragVisualPosition(dragWindow);
                        _draggedItem.GiveFeedback += feedbackHandler;

                        try { DragDrop.DoDragDrop(_draggedItem, dragData, DragDropEffects.Move); }
                        finally { _draggedItem.GiveFeedback -= feedbackHandler; dragWindow.Close(); _isDraggingNow = false; }
                    }
                }
            }
        }

        private void PdfGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PdfColumnConfig"))
            {
                var dropped = e.Data.GetData("PdfColumnConfig") as ExportColumnConfig;
                var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
                if (dropped != null && this.DataContext is ViewModels.EditorViewModel vm)
                {
                    int oldIdx = vm.PdfColumns.IndexOf(dropped);
                    int newIdx = item != null ? vm.PdfColumns.IndexOf((ExportColumnConfig)item.DataContext) : vm.PdfColumns.Count - 1;
                    if (newIdx != -1 && oldIdx != newIdx) vm.PdfColumns.Move(oldIdx, newIdx);
                }
            }
        }

        // --- Drag & Drop para Reglas PDF ---

        private void PdfRulesList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _draggedItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            _isDraggingNow = false;

            if (_draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
            }
        }

        private void PdfRulesList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingNow && _draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                PdfRulesList.SelectedItem = _draggedItem.DataContext;
            }
            _draggedItem = null;
        }

        private void PdfRulesList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDraggingNow = true;
                    var rule = _draggedItem.DataContext as PdfReplacementRule;
                    if (rule != null)
                    {
                        PdfRulesList.SelectedItem = rule;
                        DataObject dragData = new DataObject("PdfReplacementRule", rule);
                        var dragWindow = CreateDragVisual(_draggedItem, "Regla: " + rule.TargetValue);
                        dragWindow.Show();

                        System.Windows.GiveFeedbackEventHandler feedbackHandler = (s, args) => UpdateDragVisualPosition(dragWindow);
                        _draggedItem.GiveFeedback += feedbackHandler;

                        try { DragDrop.DoDragDrop(_draggedItem, dragData, DragDropEffects.Move); }
                        finally { _draggedItem.GiveFeedback -= feedbackHandler; dragWindow.Close(); _isDraggingNow = false; }
                    }
                }
            }
        }

        private void PdfRulesList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PdfReplacementRule"))
            {
                var dropped = e.Data.GetData("PdfReplacementRule") as PdfReplacementRule;
                var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
                if (dropped != null && this.DataContext is ViewModels.EditorViewModel vm && vm.EditingPauta != null)
                {
                    int oldIdx = vm.EditingPauta.PdfReplacementRules.IndexOf(dropped);
                    int newIdx = item != null ? vm.EditingPauta.PdfReplacementRules.IndexOf((PdfReplacementRule)item.DataContext) : vm.EditingPauta.PdfReplacementRules.Count - 1;
                    if (newIdx != -1 && oldIdx != newIdx) vm.EditingPauta.PdfReplacementRules.Move(oldIdx, newIdx);
                }
            }
        }

        // --- Ayudantes Visuales para Arrastre ---

        private Window CreateDragVisual(FrameworkElement source, string text)
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
                    Text = text,
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
                window.Left = lpPoint.X + 5;
                window.Top = lpPoint.Y + 5;
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
