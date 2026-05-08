using System;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;

namespace PautaDinamicaApp.Views
{
    public partial class SettingsWindow : Window
    {
        private System.Windows.Point _startPoint;
        private System.Windows.Controls.ListBoxItem? _draggedItem;
        private bool _isDraggingNow;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }
        public SettingsWindow()
        {
            InitializeComponent();
            AdminPassBox.PasswordChanged += (s, e) =>
            {
                if (DataContext is ViewModels.SettingsViewModel vm)
                {
                    vm.AdminPassword = AdminPassBox.Password;
                }
            };
        }

        private void EmailBody_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (EmailBodyTextBox != null)
            {
                double newHeight = EmailBodyTextBox.Height + e.VerticalChange;
                if (newHeight >= EmailBodyTextBox.MinHeight)
                {
                    EmailBodyTextBox.Height = newHeight;
                }
            }
        }

        // --- Drag & Drop Implementation ---

        private void EmailRulesList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _draggedItem = FindVisualParent<System.Windows.Controls.ListBoxItem>(e.OriginalSource as DependencyObject);
            _isDraggingNow = false;

            if (_draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
            }
        }

        private void EmailRulesList_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDraggingNow && _draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                EmailRulesList.SelectedItem = _draggedItem.DataContext;
            }
            _draggedItem = null;
        }

        private void EmailRulesList_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && _draggedItem != null)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDraggingNow = true;
                    var rule = _draggedItem.DataContext as Models.EmailReplacementRule;
                    if (rule != null)
                    {
                        EmailRulesList.SelectedItem = rule;
                        System.Windows.DataObject dragData = new System.Windows.DataObject("EmailReplacementRule", rule);
                        
                        var dragWindow = CreateDragVisual(_draggedItem, "Regla: " + rule.TargetValue);
                        dragWindow.Show();

                        System.Windows.GiveFeedbackEventHandler feedbackHandler = (s, args) => UpdateDragVisualPosition(dragWindow);
                        _draggedItem.GiveFeedback += feedbackHandler;

                        try { System.Windows.DragDrop.DoDragDrop(_draggedItem, dragData, System.Windows.DragDropEffects.Move); }
                        finally { _draggedItem.GiveFeedback -= feedbackHandler; dragWindow.Close(); _isDraggingNow = false; }
                    }
                }
            }
        }

        private void EmailRulesList_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("EmailReplacementRule"))
            {
                var dropped = e.Data.GetData("EmailReplacementRule") as Models.EmailReplacementRule;
                var item = FindVisualParent<System.Windows.Controls.ListBoxItem>(e.OriginalSource as DependencyObject);
                if (dropped != null && this.DataContext is ViewModels.SettingsViewModel vm && vm.SelectedPauta != null)
                {
                    int oldIdx = vm.SelectedPauta.EmailReplacementRules.IndexOf(dropped);
                    int newIdx = item != null ? vm.SelectedPauta.EmailReplacementRules.IndexOf((Models.EmailReplacementRule)item.DataContext) : vm.SelectedPauta.EmailReplacementRules.Count - 1;
                    if (newIdx != -1 && oldIdx != newIdx)
                    {
                        vm.SelectedPauta.EmailReplacementRules.Move(oldIdx, newIdx);
                    }
                }
            }
        }

        private bool IsFocusableControl(DependencyObject? obj)
        {
            if (obj == null) return false;
            var parent = obj;
            while (parent != null && !(parent is System.Windows.Controls.ListBoxItem))
            {
                if (parent is System.Windows.Controls.Button || parent is System.Windows.Controls.CheckBox || parent is System.Windows.Controls.TextBox || parent is System.Windows.Controls.ComboBox)
                    return true;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return false;
        }

        private T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            DependencyObject? parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }
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
    }
}
