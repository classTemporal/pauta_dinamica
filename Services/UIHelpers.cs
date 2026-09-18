using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PautaDinamicaApp.Services
{
    public static class UIHelpers
    {
        /// <summary>
        /// Attached property that enables mouse-wheel scrolling for a ComboBox
        /// dropdown. When a ComboBox dropdown is open, the Popup hosts a
        /// separate HWND and mouse-wheel events over items may not reach the
        /// inner ScrollViewer. This behavior, when set on a ScrollViewer,
        /// handles PreviewMouseWheel and scrolls the viewer's content so the
        /// dropdown can be navigated with the mouse wheel while open.
        /// </summary>
        public static readonly DependencyProperty EnableDropDownScrollProperty =
            DependencyProperty.RegisterAttached(
                "EnableDropDownScroll",
                typeof(bool),
                typeof(UIHelpers),
                new PropertyMetadata(false, OnEnableDropDownScrollChanged));

        public static bool GetEnableDropDownScroll(DependencyObject obj) =>
            (bool)obj.GetValue(EnableDropDownScrollProperty);

        public static void SetEnableDropDownScroll(DependencyObject obj, bool value) =>
            obj.SetValue(EnableDropDownScrollProperty, value);

        private static void OnEnableDropDownScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer scrollViewer) return;

            if ((bool)e.NewValue)
            {
                scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                scrollViewer.CanContentScroll = false;
                scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else
            {
                scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
            }
        }

        private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;

            // The PreviewMouseWheel event tunnels from the mouse-targeted
            // child up through the ScrollViewer. When the ScrollViewer is
            // inside a Popup, setting CanContentScroll=false (done in the
            // property changed callback) should make native wheel handling
            // work. As a fallback, we also explicitly scroll here and mark
            // the event as handled so the parent window behind the dropdown
            // does not scroll instead.
            if (scrollViewer.ExtentHeight > scrollViewer.ActualHeight)
            {
                var delta = e.Delta;
                if (delta > 0)
                    scrollViewer.LineUp();
                else
                    scrollViewer.LineDown();

                e.Handled = true;
            }
        }

        public static readonly DependencyProperty EnableHeightResizeProperty =
            DependencyProperty.RegisterAttached(
                "EnableHeightResize",
                typeof(bool),
                typeof(UIHelpers),
                new PropertyMetadata(false, OnEnableHeightResizeChanged));

        public static bool GetEnableHeightResize(DependencyObject obj) => (bool)obj.GetValue(EnableHeightResizeProperty);
        public static void SetEnableHeightResize(DependencyObject obj, bool value) => obj.SetValue(EnableHeightResizeProperty, value);

        private static void OnEnableHeightResizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Thumb thumb && (bool)e.NewValue)
            {
                thumb.DragDelta += Thumb_DragDelta;
            }
            else if (d is Thumb oldThumb && !(bool)e.NewValue)
            {
                oldThumb.DragDelta -= Thumb_DragDelta;
            }
        }

        private static void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb)
            {
                // Buscamos el TextBox hermano o padre
                var parent = thumb.Parent as FrameworkElement;
                if (parent == null) return;

                // Intentamos encontrar un TextBox en el mismo contenedor
                var textBox = FindChild<System.Windows.Controls.TextBox>(parent);
                if (textBox != null)
                {
                    double newHeight = textBox.ActualHeight + e.VerticalChange;
                    if (newHeight >= 50) // Mínimo razonable
                    {
                        textBox.Height = newHeight;
                    }
                }
            }
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
