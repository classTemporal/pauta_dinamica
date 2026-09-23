using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PautaDinamicaApp.ViewModels;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace PautaDinamicaApp
{
    public partial class MainWindow : Window
    {
        private System.Windows.Point _dashboardDragStartPoint;
        private ContentPresenter? _dashboardDraggedItem;
        private bool _isDashboardDraggingNow;
        private ScrollViewer? _dashboardScrollViewer;
        private DispatcherTimer? _dashboardAutoScrollTimer;

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

        // --- Auto-scroll helpers (Dashboard) ---

        private void StartDashboardAutoScroll()
        {
            _dashboardScrollViewer = DashboardScrollViewer;
            if (_dashboardScrollViewer == null) return;

            _dashboardAutoScrollTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            _dashboardAutoScrollTimer.Tick -= DashboardAutoScrollTimer_Tick;
            _dashboardAutoScrollTimer.Tick += DashboardAutoScrollTimer_Tick;
            _dashboardAutoScrollTimer.Start();
        }

        private void StopDashboardAutoScroll()
        {
            if (_dashboardAutoScrollTimer != null)
            {
                _dashboardAutoScrollTimer.Stop();
                _dashboardAutoScrollTimer.Tick -= DashboardAutoScrollTimer_Tick;
                _dashboardAutoScrollTimer = null;
            }
            _dashboardScrollViewer = null;
        }

        private void DashboardAutoScrollTimer_Tick(object? sender, EventArgs e)
        {
            if (_dashboardScrollViewer == null) return;
            var sv = _dashboardScrollViewer;

            System.Windows.Point cursor = Mouse.GetPosition(sv);
            double height = sv.ActualHeight;
            const double edgeThreshold = 40.0;

            if (cursor.Y < edgeThreshold)
            {
                sv.LineUp();
            }
            else if (cursor.Y > height - edgeThreshold)
            {
                sv.LineDown();
            }
        }

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

        // --- Dashboard ItemsControl handlers ---

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
                            System.Windows.DragDrop.DoDragDrop(_dashboardDraggedItem, dragData, System.Windows.DragDropEffects.Move);
                        }
                        finally
                        {
                            _isDashboardDraggingNow = false;
                            StopDashboardAutoScroll();
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
                StartDashboardAutoScroll();
            }
            e.Handled = true;
        }

        private void DashboardScrollViewer_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("DynamicFieldVM")) return;

            var droppedField = e.Data.GetData("DynamicFieldVM") as DynamicFieldVM;
            if (droppedField == null) return;

            StopDashboardAutoScroll();

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