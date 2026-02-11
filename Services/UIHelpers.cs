using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PautaDinamicaApp.Services
{
    public static class UIHelpers
    {
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
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
