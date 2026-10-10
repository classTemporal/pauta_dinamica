using System;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using PautaDinamicaApp.Services;

namespace PautaDinamicaApp.Views
{
    public partial class SettingsWindow : Window
    {
        private System.Windows.Point _startPoint;
        private System.Windows.Controls.ListBoxItem? _draggedItem;
        private bool _isDraggingNow;
        private int _initialTabIndex = 0;

        public int InitialTabIndex
        {
            get => _initialTabIndex;
            set
            {
                _initialTabIndex = value;
                // Object initializers run AFTER the constructor, so the tab selection
                // must happen here for property-set values to take effect.
                if (MainTabControl != null && _initialTabIndex > 0
                    && _initialTabIndex < MainTabControl.Items.Count)
                    MainTabControl.SelectedIndex = _initialTabIndex;
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        /// <summary>Abre los enlaces de "Sobre esta aplicación" en el navegador.</summary>
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("no se pudo abrir el enlace: " + ex.Message);
            }
            e.Handled = true;
        }
        public SettingsWindow()
        {
            InitializeComponent();
            if (InitialTabIndex > 0 && InitialTabIndex < MainTabControl.Items.Count)
                MainTabControl.SelectedIndex = InitialTabIndex;

            AdminPassBox.PasswordChanged += (s, e) =>
            {
                if (DataContext is ViewModels.SettingsViewModel vm)
                {
                    vm.AdminPassword = AdminPassBox.Password;
                }
            };

            // Vuelca el HTML del editor a la pauta antes de cada guardado.
            // Se engancha en DataContextChanged porque la ventana se crea con un
            // inicializador de objeto (DataContext = vm), es decir, después del ctor.
            DataContextChanged += (s, args) =>
            {
                if (args.OldValue is ViewModels.SettingsViewModel oldVm) oldVm.FlushRequested -= FlushHtmlBodyAsync;
                if (args.NewValue is ViewModels.SettingsViewModel newVm) newVm.FlushRequested += FlushHtmlBodyAsync;
            };

            if (DataContext is ViewModels.SettingsViewModel initialVm)
            {
                initialVm.FlushRequested += FlushHtmlBodyAsync;
            }
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

        /// <summary>
        /// Vuelca el HTML de los editores nativos a sus propiedades antes de guardar.
        /// Recorre el árbol visual porque las reglas adicionales viven en un ItemsControl.
        /// </summary>
        public async System.Threading.Tasks.Task FlushHtmlBodyAsync()
        {
            foreach (var ed in FindVisualChildren<HtmlEditor.IHtmlEditor>(this))
                await ed.FlushAsync();
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : class
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var sub in FindVisualChildren<T>(child)) yield return sub;
            }
        }

        private System.Collections.Generic.IEnumerable<Models.FieldDefinition> PautaFields()
            => (DataContext as ViewModels.SettingsViewModel)?.CurrentPautaFields
               ?? System.Linq.Enumerable.Empty<Models.FieldDefinition>();

        private Models.DynamicDateConfig? CurrentDynamicDates()
            => (DataContext as ViewModels.SettingsViewModel)?.SelectedPauta?.DynamicDates;

        /// <summary>Inserta el marcador donde está el cursor del cuadro de texto plano.</summary>
        private void InsertAtCaret(System.Windows.Controls.TextBox box, string placeholder)
        {
            if (box == null) return;

            int caret = box.SelectionStart;
            box.Text = box.Text.Insert(caret, placeholder);
            box.SelectionStart = caret + placeholder.Length;
            box.SelectionLength = 0;
            box.Focus();
        }

        private async void InsertFieldButton_Click(object sender, RoutedEventArgs e)
        {
            string? placeholder = Views.FieldPickerPopup.Pick(PautaFields(), this);
            if (string.IsNullOrEmpty(placeholder)) return;
            if ((DataContext as ViewModels.SettingsViewModel)?.SelectedPauta?.EmailMethod
                == Models.EmailMethod.Outlook)
            {
                await HtmlBodyEditor.InsertTextAtCaretAsync(placeholder);
                return;
            }
            InsertAtCaret(EmailBodyTextBox, placeholder);
        }

        private async void InsertDateButton_Click(object sender, RoutedEventArgs e)
        {
            var picked = Views.DateTokenPickerPopup.Pick(CurrentDynamicDates(), this);
            if (picked == null) return;

            var vm = DataContext as ViewModels.SettingsViewModel;
            if (picked.Value.Config != null && vm?.SelectedPauta != null)
                vm.SelectedPauta.DynamicDates = picked.Value.Config;

            if (vm?.SelectedPauta?.EmailMethod == Models.EmailMethod.Outlook)
            {
                await HtmlBodyEditor.InsertTextAtCaretAsync(picked.Value.Token);
                return;
            }
            InsertAtCaret(EmailBodyTextBox, picked.Value.Token);
        }

        private void InsertSubjectField_Click(object sender, RoutedEventArgs e)
        {
            string? placeholder = Views.FieldPickerPopup.Pick(PautaFields(), this);
            if (!string.IsNullOrEmpty(placeholder)) InsertAtCaret(EmailSubjectTextBox, placeholder);
        }

        private void InsertSubjectDate_Click(object sender, RoutedEventArgs e)
        {
            var picked = Views.DateTokenPickerPopup.Pick(CurrentDynamicDates(), this);
            if (picked == null) return;

            var vm = DataContext as ViewModels.SettingsViewModel;
            if (picked.Value.Config != null && vm?.SelectedPauta != null)
                vm.SelectedPauta.DynamicDates = picked.Value.Config;

            InsertAtCaret(EmailSubjectTextBox, picked.Value.Token);
        }

        private System.Windows.Controls.TextBox? FindRuleSubjectBox(DependencyObject start)
        {
            DependencyObject? cur = start;
            while (cur != null && cur is not System.Windows.Controls.Border)
                cur = System.Windows.Media.VisualTreeHelper.GetParent(cur);
            if (cur == null) return null;
            foreach (var box in FindVisualChildren<System.Windows.Controls.TextBox>(cur))
                if ((box.Tag as string) == "RuleSubjectBox") return box;
            return null;
        }

        private void RuleSubjectField_Click(object sender, RoutedEventArgs e)
        {
            var box = FindRuleSubjectBox((DependencyObject)sender);
            if (box == null) return;
            string? placeholder = Views.FieldPickerPopup.Pick(PautaFields(), this);
            if (!string.IsNullOrEmpty(placeholder)) InsertAtCaret(box, placeholder);
        }

        private void RuleSubjectDate_Click(object sender, RoutedEventArgs e)
        {
            var box = FindRuleSubjectBox((DependencyObject)sender);
            if (box == null) return;
            var picked = Views.DateTokenPickerPopup.Pick(CurrentDynamicDates(), this);
            if (picked == null) return;

            var vm = DataContext as ViewModels.SettingsViewModel;
            if (picked.Value.Config != null && vm?.SelectedPauta != null)
                vm.SelectedPauta.DynamicDates = picked.Value.Config;

            InsertAtCaret(box, picked.Value.Token);
        }

        private System.Windows.DependencyObject? FindRuleCard(System.Windows.DependencyObject start)
        {
            System.Windows.DependencyObject? cur = start;
            while (cur != null && cur is not System.Windows.Controls.Border)
                cur = System.Windows.Media.VisualTreeHelper.GetParent(cur);
            return cur;
        }

        private async void RuleBodyField_Click(object sender, RoutedEventArgs e)
        {
            var card = FindRuleCard((System.Windows.DependencyObject)sender);
            if (card == null) return;
            string? placeholder = Views.FieldPickerPopup.Pick(PautaFields(), this);
            if (string.IsNullOrEmpty(placeholder)) return;
            foreach (var ed in FindVisualChildren<HtmlEditor.IHtmlEditor>(card))
                if (ed.Visibility == System.Windows.Visibility.Visible)
                {
                    await ed.InsertTextAtCaretAsync(placeholder);
                    return;
                }
            foreach (var box in FindVisualChildren<System.Windows.Controls.TextBox>(card))
                if ((box.Tag as string) == "RuleBodyBox")
                {
                    InsertAtCaret(box, placeholder);
                    return;
                }
        }

        private async void RuleBodyDate_Click(object sender, RoutedEventArgs e)
        {
            var card = FindRuleCard((System.Windows.DependencyObject)sender);
            if (card == null) return;
            var picked = Views.DateTokenPickerPopup.Pick(CurrentDynamicDates(), this);
            if (picked == null) return;

            var vm = DataContext as ViewModels.SettingsViewModel;
            if (picked.Value.Config != null && vm?.SelectedPauta != null)
                vm.SelectedPauta.DynamicDates = picked.Value.Config;

            foreach (var ed in FindVisualChildren<HtmlEditor.IHtmlEditor>(card))
                if (ed.Visibility == System.Windows.Visibility.Visible)
                {
                    await ed.InsertTextAtCaretAsync(picked.Value.Token);
                    return;
                }
            foreach (var box in FindVisualChildren<System.Windows.Controls.TextBox>(card))
                if ((box.Tag as string) == "RuleBodyBox")
                {
                    InsertAtCaret(box, picked.Value.Token);
                    return;
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

                        DragScrollHelper.Current.BeginDrag(EmailRulesList);
                        try { System.Windows.DragDrop.DoDragDrop(_draggedItem, dragData, System.Windows.DragDropEffects.Move); }
                        finally { _draggedItem.GiveFeedback -= feedbackHandler; dragWindow.Close(); _isDraggingNow = false; DragScrollHelper.Current.Stop(); }
                    }
                }
            }
        }

        private void EmailRulesList_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (!_isDraggingNow) return;
            if (sender is FrameworkElement listBox)
            {
                DragScrollHelper.Current.Update(e, listBox);
            }
            e.Handled = true;
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
