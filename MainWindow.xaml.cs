using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
                    // Encadenar la rueda: los ScrollViewers internos (TextBox
                    // multilínea, tabla, etc.) tragan el evento aunque ya no
                    // puedan avanzar; si el interno está en su límite, mueve
                    // la página en su lugar.
                    DashboardScrollViewer.PreviewMouseWheel += PageScrollViewer_PreviewMouseWheel;
                    SinglePageRoot.PreviewMouseWheel += PageScrollViewer_PreviewMouseWheel;
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

        /// <summary>
        /// Encadena el scroll de la página con los ScrollViewers internos.
        /// El handler corre en túnel (antes que los internos): si bajo el cursor
        /// hay un scroll interno que aún puede avanzar en esa dirección, se deja
        /// pasar; si está en su límite (o no hay ninguno), se mueve la página y
        /// se marca como manejado para que el interno no trague la rueda.
        /// </summary>
        private void PageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer outer) return;
            if (outer.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled) return;

            // Dentro de un Popup abierto (desplegable de ComboBox, calendario):
            // no interferir, ese contenido se desplaza solo.
            DependencyObject? current = e.OriginalSource as DependencyObject;
            while (current != null && current != outer)
            {
                if (current is System.Windows.Controls.Primitives.Popup) return;
                if (current is ScrollViewer inner &&
                    inner != outer &&
                    inner.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
                {
                    bool canScroll = e.Delta < 0
                        ? inner.VerticalOffset < inner.ScrollableHeight - 0.5
                        : inner.VerticalOffset > 0.5;
                    if (canScroll) return; // el interno aún puede: dejarlo pasar
                }
                current = GetParentSafe(current);
            }

            e.Handled = true;
            outer.ScrollToVerticalOffset(outer.VerticalOffset - e.Delta);
        }

        /// <summary>
        /// Sube un nivel en el árbol. El origen del evento puede ser un elemento
        /// de contenido (ej: Run del texto de un campo), que no es Visual y con
        /// el que VisualTreeHelper.GetParent lanza InvalidOperationException:
        /// esos se resuelven por el árbol lógico.
        /// </summary>
        private static DependencyObject? GetParentSafe(DependencyObject child)
        {
            if (child is Visual || child is System.Windows.Media.Media3D.Visual3D)
            {
                try { return VisualTreeHelper.GetParent(child); }
                catch (InvalidOperationException) { return LogicalTreeHelper.GetParent(child); }
            }
            if (child is FrameworkContentElement fce && fce.Parent != null)
                return fce.Parent;
            return LogicalTreeHelper.GetParent(child);
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