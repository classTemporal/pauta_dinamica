using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PautaDinamicaApp.ViewModels;
using PautaDinamicaApp.Services;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace PautaDinamicaApp
{
    public partial class MainWindow : Window
    {
        private System.Windows.Point _dashboardDragStartPoint;
        private ContentPresenter? _dashboardDraggedItem;
        private bool _isDashboardDraggingNow;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.FieldsRefreshed += RebuildColumns;
                    RebuildColumns();
                    vm.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(MainViewModel.DashboardLayout)
                            || args.PropertyName == nameof(MainViewModel.IsSinglePageLayout)
                            || args.PropertyName == nameof(MainViewModel.IsSplitLayout))
                        {
                            ApplyDashboardLayoutMode();
                        }
                    };
                    RecordsGrid.PreviewMouseWheel += RecordsGrid_ForwardWheelToPage;
                    ApplyDashboardLayoutMode();
                }
            };
        }

        private bool _singlePageApplied;

        private void ApplyDashboardLayoutMode()
        {
            if (DataContext is not MainViewModel vm) return;
            if (SplitRoot == null || SinglePageRoot == null || SinglePageStack == null) return;
            if (FormPanel == null || RecordsPanel == null) return;

            bool single = vm.IsSinglePageLayout;

            // Guardia anti-contaminación: si NO es página única y nunca entramos
            // a ella, no tocar absolutamente nada (los triggers XAML mandan).
            if (!single && !_singlePageApplied)
            {
                SplitRoot.Visibility = Visibility.Visible;
                SinglePageRoot.Visibility = Visibility.Collapsed;
                // Restaurar scrolls a sus defaults del XAML (por si el modo
                // página los dejó desactivados y el usuario vuelve aquí).
                DashboardScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
                DashboardScrollViewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                // El wrapper NO debe competir con el scroll interno del DataGrid:
                // desactivado para que la rueda llegue a la tabla en split modes.
                if (RecordsScrollWrapper != null)
                    RecordsScrollWrapper.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                RecordsGrid.ClearValue(System.Windows.Controls.DataGrid.MaxHeightProperty);
                RecordsGrid.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
                return;
            }

            if (single && !_singlePageApplied)
            {
                if (FormPanel.Parent is System.Windows.Controls.Panel fp) fp.Children.Remove(FormPanel);
                if (RecordsPanel.Parent is System.Windows.Controls.Panel rp) rp.Children.Remove(RecordsPanel);
                // Campos arriba, auditorías abajo, en una sola columna.
                FormPanel.Margin = new Thickness(0);
                SinglePageStack.Children.Add(FormPanel);
                SinglePageStack.Children.Add(RecordsPanel);
                RecordsPanel.Margin = new Thickness(0, 20, 0, 0);
                _singlePageApplied = true;
            }
            else if (!single && _singlePageApplied)
            {
                if (FormPanel.Parent is System.Windows.Controls.Panel p1) p1.Children.Remove(FormPanel);
                if (RecordsPanel.Parent is System.Windows.Controls.Panel p2) p2.Children.Remove(RecordsPanel);
                SinglePageStack.Children.Clear();
                SplitRoot.Children.Add(FormPanel);
                SplitRoot.Children.Add(RecordsPanel);

                // Las posiciones las controlan los DataTriggers del XAML: limpiar
                // valores locales para no anularlos permanentemente.
                FormPanel.ClearValue(System.Windows.Controls.Grid.RowProperty);
                FormPanel.ClearValue(System.Windows.Controls.Grid.ColumnProperty);
                FormPanel.ClearValue(System.Windows.Controls.Grid.RowSpanProperty);
                FormPanel.ClearValue(System.Windows.Controls.Grid.ColumnSpanProperty);
                FormPanel.ClearValue(MarginProperty);
                RecordsPanel.ClearValue(System.Windows.Controls.Grid.RowProperty);
                RecordsPanel.ClearValue(System.Windows.Controls.Grid.ColumnProperty);
                RecordsPanel.ClearValue(System.Windows.Controls.Grid.RowSpanProperty);
                RecordsPanel.ClearValue(System.Windows.Controls.Grid.ColumnSpanProperty);
                RecordsPanel.ClearValue(MarginProperty);
                // Restaurar scrolls a sus defaults. El wrapper queda Disabled para
                // no robar la rueda al scroll interno del DataGrid.
                DashboardScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
                DashboardScrollViewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                if (RecordsScrollWrapper != null)
                    RecordsScrollWrapper.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                RecordsGrid.ClearValue(System.Windows.Controls.DataGrid.MaxHeightProperty);
                RecordsGrid.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
                _singlePageApplied = false;
            }

            if (single)
            {
                // Página única: ocultar el grid con splitter (vacío) para que no
                // compita por el mismo espacio, y mostrar la página única.
                SplitRoot.Visibility = Visibility.Collapsed;
                SinglePageRoot.Visibility = Visibility.Visible;
                DashboardScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                DashboardScrollViewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                if (RecordsScrollWrapper != null)
                    RecordsScrollWrapper.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                RecordsGrid.MaxHeight = 600;
                RecordsGrid.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            }
            else
            {
                // Modos con splitter: restaurar valores por defecto.
                // Wrapper Disabled: la rueda va directo al DataGrid.
                SplitRoot.Visibility = Visibility.Visible;
                SinglePageRoot.Visibility = Visibility.Collapsed;
                DashboardScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
                DashboardScrollViewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                if (RecordsScrollWrapper != null)
                    RecordsScrollWrapper.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                RecordsGrid.ClearValue(System.Windows.Controls.DataGrid.MaxHeightProperty);
                RecordsGrid.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            }
        }

        /// <summary>
        /// En modo "Una ventana", la rueda sobre la tabla quedaba atrapada por el
        /// ScrollViewer interno del DataGrid. Cuando la tabla ya no puede avanzar
        /// en esa dirección, se reenvía el evento al scroll de la página.
        /// </summary>
        private void RecordsGrid_ForwardWheelToPage(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (DataContext is not MainViewModel vm || !vm.IsSinglePageLayout) return;
            if (SinglePageRoot == null) return;

            var inner = FindVisualChild<System.Windows.Controls.ScrollViewer>(RecordsGrid);
            bool atLimit = true;
            if (inner != null && inner.ScrollableHeight > 0)
            {
                if (e.Delta < 0) atLimit = inner.VerticalOffset >= inner.ScrollableHeight - 0.5;
                else atLimit = inner.VerticalOffset <= 0.5;
            }

            if (atLimit)
            {
                e.Handled = true;
                var forwarded = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = System.Windows.UIElement.MouseWheelEvent,
                    Source = SinglePageRoot
                };
                SinglePageRoot.RaiseEvent(forwarded);
            }
            // Si no está en el límite, se deja que la tabla haga scroll con normalidad.
        }

        // --- Dashboard ItemsControl handlers ---

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void DashboardItemsControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dashboardDragStartPoint = e.GetPosition(null);
            _dashboardDraggedItem = FindVisualChild<ContentPresenter>(e.OriginalSource as DependencyObject);
            _isDashboardDraggingNow = false;

            if (_dashboardDraggedItem != null)
            {
                e.Handled = true;
            }
        }

        private void DashboardItemsControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dashboardDraggedItem = null;
        }

        private void DashboardItemsControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _dashboardDraggedItem != null)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                Vector diff = _dashboardDragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDashboardDraggingNow = true;
                    var field = _dashboardDraggedItem.DataContext as DynamicFieldVM;
                    if (field != null)
                    {
                        System.Windows.DataObject dragData = new System.Windows.DataObject("DynamicFieldVM", field);

                        try
                        {
                            DragScrollHelper.Current.BeginDrag(DashboardScrollViewer);
                            System.Windows.DragDrop.DoDragDrop(_dashboardDraggedItem, dragData, System.Windows.DragDropEffects.Move);
                        }
                        finally
                        {
                            _isDashboardDraggingNow = false;
                            DragScrollHelper.Current.Stop();
                            _dashboardDraggedItem = null;
                        }
                    }
                }
            }
        }

        private void DashboardScrollViewer_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("DynamicFieldVM"))
                e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
        }

        private void DashboardScrollViewer_DragOver(object sender, DragEventArgs e)
        {
            if (!_isDashboardDraggingNow) return;
            if (e.Data.GetDataPresent("DynamicFieldVM"))
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                DragScrollHelper.Current.Update(e, DashboardScrollViewer);
            }
            e.Handled = true;
        }

        private void DashboardScrollViewer_DragLeave(object sender, DragEventArgs e)
        {
            // Si el cursor sale del área sin soltar, frenar el auto-scroll
            // (el hook de la rueda sigue activo hasta soltar el botón).
            DragScrollHelper.Current.PauseAutoScroll();
            e.Handled = true;
        }

        private void DashboardScrollViewer_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("DynamicFieldVM")) return;

            var droppedField = e.Data.GetData("DynamicFieldVM") as DynamicFieldVM;
            if (droppedField == null) return;

            DragScrollHelper.Current.Stop();

            if (DataContext is not MainViewModel vm) return;

            var fields = vm.CurrentFields;
            int oldIndex = fields.IndexOf(droppedField);
            if (oldIndex == -1) return;

            // Determine new index based on cursor position relative to items
            System.Windows.Point dropPos = e.GetPosition(DashboardItemsControl);
            double totalHeight = DashboardItemsControl.ActualHeight;
            if (totalHeight <= 0) totalHeight = 1;

            // Find the item under the cursor or estimate by position
            int newIndex = -1;
            for (int i = 0; i < DashboardItemsControl.Items.Count; i++)
            {
                var container = DashboardItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container != null)
                {
                    double top = container.TransformToAncestor(DashboardItemsControl).Transform(new System.Windows.Point(0, 0)).Y;
                    double bottom = top + container.ActualHeight;
                    double mid = (top + bottom) / 2;
                    if (dropPos.Y < mid)
                    {
                        newIndex = i;
                        break;
                    }
                }
            }
            if (newIndex == -1) newIndex = DashboardItemsControl.Items.Count - 1;

            if (newIndex != oldIndex)
            {
                fields.Move(oldIndex, newIndex);
                vm.SaveDashboardFieldOrder();
            }

            e.Handled = true;
        }

        private void RebuildColumns()
        {
            if (DataContext is not MainViewModel vm) return;
            if (RecordsGrid == null) return;

            while (RecordsGrid.Columns.Count > 2)
            {
                RecordsGrid.Columns.RemoveAt(2);
            }

            foreach (var field in vm.CurrentFields)
            {
                var column = new DataGridTextColumn
                {
                    Header = field.Label,
                    Binding = new System.Windows.Data.Binding("Values")
                    {
                        Converter = (IValueConverter)System.Windows.Application.Current.Resources["DictionaryValueConverter"],
                        ConverterParameter = field.Id
                    },
                    Width = new DataGridLength(150),
                    MinWidth = 180,
                    CanUserSort = false
                };

                RecordsGrid.Columns.Add(column);
            }
        }
    }
}