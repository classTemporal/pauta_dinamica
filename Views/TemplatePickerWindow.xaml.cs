using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;

namespace PautaDinamicaApp.Views
{
    public partial class TemplatePickerWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<MessageTemplate> _templates;
        private readonly StorageService _storageService;
        private bool _isMultiSelectMode;
        private System.Windows.Point _startPoint;
        private System.Windows.Controls.ListBoxItem? _draggedItem;
        private bool _isDraggingNow;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public string SelectedTemplateContent { get; private set; } = string.Empty;

        public bool IsValid => true;
        
        public bool IsMultiSelectMode
        {
            get => _isMultiSelectMode;
            set
            {
                if (_isMultiSelectMode != value)
                {
                    _isMultiSelectMode = value;
                    if (!value)
                    {
                        foreach (var t in _templates) t.IsSelected = false;
                    }
                    OnPropertyChanged();
                }
            }
        }

        public TemplatePickerWindow(List<MessageTemplate> templates)
        {
            InitializeComponent();
            _templates = new ObservableCollection<MessageTemplate>(templates);
            _storageService = new StorageService();
            TemplatesList.ItemsSource = _templates;
            DataContext = this;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (TemplatesList.SelectedItem is MessageTemplate template)
            {
                SelectedTemplateContent = template.Content;
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Por favor, selecciona una plantilla de la lista.", "Selección Requerida");
            }
        }

        private void TemplatesList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // No insertar si está en modo multi-select o si el clic fue en un botón/control
            if (IsMultiSelectMode || _isDraggingNow) return;
            if (IsFocusableControl(e.OriginalSource as DependencyObject)) return;

            if (TemplatesList.SelectedItem is MessageTemplate template)
            {
                SelectedTemplateContent = template.Content;
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowAdd_Click(object sender, RoutedEventArgs e)
        {
            AddTemplateGrid.Visibility = Visibility.Visible;
            ShowAddPanel.Visibility = Visibility.Collapsed;
            NewTemplateTextBox.Focus();
        }

        private void CancelAdd_Click(object sender, RoutedEventArgs e)
        {
            AddTemplateGrid.Visibility = Visibility.Collapsed;
            ShowAddPanel.Visibility = Visibility.Visible;
            NewTemplateTextBox.Clear();
        }

        private void SaveNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            string content = NewTemplateTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                System.Windows.MessageBox.Show("El contenido de la plantilla no puede estar vacío.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newTemplate = new MessageTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Content = content
            };

            _templates.Add(newTemplate);
            SaveCurrentState();

            // Reset UI
            NewTemplateTextBox.Clear();
            AddTemplateGrid.Visibility = Visibility.Collapsed;
            ShowAddPanel.Visibility = Visibility.Visible;

            // Auto-select the new one
            TemplatesList.SelectedItem = newTemplate;
            TemplatesList.ScrollIntoView(newTemplate);
        }

        private void SaveCurrentState()
        {
            _storageService.SaveTemplates(_templates.ToList());
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            var selected = _templates.Where(t => t.IsSelected).ToList();
            if (!selected.Any())
            {
                if (sender is FrameworkElement fe && fe.DataContext is MessageTemplate t)
                    selected.Add(t);
                else if (TemplatesList.SelectedItem is MessageTemplate st)
                    selected.Add(st);
                else return;
            }

            var orderedSelected = selected.OrderBy(t => _templates.IndexOf(t)).ToList();
            foreach (var t in orderedSelected)
            {
                int idx = _templates.IndexOf(t);
                if (idx > 0 && !_templates[idx - 1].IsSelected)
                {
                    _templates.Move(idx, idx - 1);
                }
            }
            SaveCurrentState();
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            var selected = _templates.Where(t => t.IsSelected).ToList();
            if (!selected.Any())
            {
                if (sender is FrameworkElement fe && fe.DataContext is MessageTemplate t)
                    selected.Add(t);
                else if (TemplatesList.SelectedItem is MessageTemplate st)
                    selected.Add(st);
                else return;
            }

            var orderedSelected = selected.OrderByDescending(t => _templates.IndexOf(t)).ToList();
            foreach (var t in orderedSelected)
            {
                int idx = _templates.IndexOf(t);
                if (idx < _templates.Count - 1 && !_templates[idx + 1].IsSelected)
                {
                    _templates.Move(idx, idx + 1);
                }
            }
            SaveCurrentState();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool all = _templates.All(t => t.IsSelected);
            foreach (var t in _templates) t.IsSelected = !all;
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _templates.Where(t => t.IsSelected).ToList();
            if (selected.Any() && System.Windows.MessageBox.Show($"¿Eliminar {selected.Count} plantillas?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var t in selected) _templates.Remove(t);
                SaveCurrentState();
            }
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            var vm = new ViewModels.TemplateManagementViewModel();
            var win = new TemplateManagementWindow { DataContext = vm, Owner = this };
            
            // Suscribir al cierre si es necesario, o simplemente recargar al volver
            win.ShowDialog();

            // Recargar plantillas por si hubo cambios en la otra ventana
            var updated = _storageService.LoadTemplates();
            _templates.Clear();
            foreach (var t in updated) _templates.Add(t);
        }

        // --- Drag & Drop Implementation ---

        private void TemplatesList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _draggedItem = FindVisualParent<System.Windows.Controls.ListBoxItem>(e.OriginalSource as DependencyObject);
            _isDraggingNow = false;

            if (_draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
            }
        }

        private void TemplatesList_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDraggingNow && _draggedItem != null && !IsFocusableControl(e.OriginalSource as DependencyObject))
            {
                TemplatesList.SelectedItem = _draggedItem.DataContext;
            }
            _draggedItem = null;
        }

        private void TemplatesList_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && _draggedItem != null)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDraggingNow = true;
                    var template = _draggedItem.DataContext as MessageTemplate;
                    if (template != null)
                    {
                        TemplatesList.SelectedItem = template;
                        System.Windows.DataObject dragData = new System.Windows.DataObject("MessageTemplate", template);
                        
                        var dragWindow = CreateDragVisual(_draggedItem, "Plantilla: " + template.Content);
                        dragWindow.Show();

                        System.Windows.GiveFeedbackEventHandler feedbackHandler = (s, args) => UpdateDragVisualPosition(dragWindow);
                        _draggedItem.GiveFeedback += feedbackHandler;

                        try { System.Windows.DragDrop.DoDragDrop(_draggedItem, dragData, System.Windows.DragDropEffects.Move); }
                        finally { _draggedItem.GiveFeedback -= feedbackHandler; dragWindow.Close(); _isDraggingNow = false; }
                    }
                }
            }
        }

        private void TemplatesList_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("MessageTemplate"))
            {
                var dropped = e.Data.GetData("MessageTemplate") as MessageTemplate;
                var item = FindVisualParent<System.Windows.Controls.ListBoxItem>(e.OriginalSource as DependencyObject);
                if (dropped != null)
                {
                    int oldIdx = _templates.IndexOf(dropped);
                    int newIdx = item != null ? _templates.IndexOf((MessageTemplate)item.DataContext) : _templates.Count - 1;
                    if (newIdx != -1 && oldIdx != newIdx)
                    {
                        _templates.Move(oldIdx, newIdx);
                        SaveCurrentState();
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
