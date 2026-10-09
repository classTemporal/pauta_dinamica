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
                }
            };
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