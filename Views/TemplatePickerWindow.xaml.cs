using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using PautaDinamicaApp;
using PautaDinamicaApp.Services;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views
{
    public partial class TemplatePickerWindow : Window, INotifyPropertyChanged
    {
        private readonly StorageService _storageService;
        private readonly string _pautaId;
        private readonly PautaSchema? _currentPauta;

        // Templates filtered (flat list)
        private ObservableCollection<MessageTemplate> _displayedTemplates = new();
        private readonly ObservableCollection<MessageTemplate> _allTemplates = new();

        // Search
        private string _searchText = string.Empty;

        // Category filter
        private string _selectedCategory = "Todas";
        private readonly ObservableCollection<string> _availableCategories = new() { "Todas" };

        // Misc
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
                        foreach (var t in _allTemplates) t.IsSelected = false;
                    }
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<MessageTemplate> DisplayedTemplates
        {
            get => _displayedTemplates;
            set { _displayedTemplates = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    ApplyFilters();
                }
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value ?? "Todas";
                    OnPropertyChanged();
                    ApplyFilters();
                }
            }
        }

        public ObservableCollection<string> AvailableCategories
        {
            get => _availableCategories;
        }

        public string CurrentPautaName
        {
            get => _currentPauta?.Name ?? (_pautaId == "default" ? "Todas las Pautas" : _pautaId);
        }

        public TemplatePickerWindow(string pautaId)
        {
            InitializeComponent();
            _storageService = new StorageService();
            _pautaId = pautaId;

            // Load the pauta schema to get its name for display
            var allPautas = _storageService.LoadPautas();
            _currentPauta = allPautas.FirstOrDefault(p => p.Id == _pautaId);

            LoadTemplates();
            LoadCategories();
            DataContext = this;
        }

        private void LoadTemplates()
        {
            // Load templates for this pauta, falling back to global if none exist
            var pautaTemplates = _storageService.LoadTemplatesForPauta(_pautaId);

            // If no pauta-specific templates, fall back to global templates (PautaId == "")
            if (!pautaTemplates.Any())
            {
                pautaTemplates = _storageService.LoadTemplates().Where(t => string.IsNullOrEmpty(t.PautaId)).ToList();
            }

            _allTemplates.Clear();
            foreach (var t in pautaTemplates) _allTemplates.Add(t);

            // Initial display: all templates (filtered by search if any)
            ApplyFilters();
        }

        private void LoadCategories()
        {
            _availableCategories.Clear();
            _availableCategories.Add("Todas");

            // Load from persisted categories
            var persisted = _storageService.LoadTemplateCategories();
            foreach (var c in persisted.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            {
                if (!_availableCategories.Contains(c, StringComparer.OrdinalIgnoreCase))
                    _availableCategories.Add(c);
            }

            // Also add categories from existing templates
            var templateCats = _allTemplates
                .Where(t => !string.IsNullOrWhiteSpace(t.Category))
                .Select(t => t.Category!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);
            foreach (var c in templateCats)
            {
                if (!_availableCategories.Contains(c, StringComparer.OrdinalIgnoreCase))
                    _availableCategories.Add(c);
            }
        }

        private void ApplyFilters()
        {
            string search = _searchText?.ToLower() ?? "";
            bool hasSearch = !string.IsNullOrWhiteSpace(search);
            string category = _selectedCategory;

            IEnumerable<MessageTemplate> filtered = _allTemplates;

            // Apply category filter
            if (category != "Todas" && !string.IsNullOrWhiteSpace(category))
            {
                if (category == "Sin Categorizar")
                {
                    filtered = filtered.Where(t => string.IsNullOrWhiteSpace(t.Category));
                }
                else
                {
                    filtered = filtered.Where(t =>
                        string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Apply search filter on top
            if (hasSearch)
            {
                filtered = filtered.Where(t => t.Content?.ToLower().Contains(search) == true);
            }

            // Update DisplayedTemplates in-place to keep the ListBox bound to the same ObservableCollection
            _displayedTemplates.Clear();
            foreach (var t in filtered) _displayedTemplates.Add(t);
        }

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectedCategory is bound TwoWay, ApplyFilters runs automatically via setter
            ApplyFilters();
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
                MessageBoxHelper.Show("Por favor, selecciona una plantilla de la lista.", "Selección Requerida", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TemplatesList_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
            if (IsMultiSelectMode || _isDraggingNow) return;
            if (IsFocusableControl(e.OriginalSource as DependencyObject)) return;

            // Resolve the double-clicked item from event source
            var listBoxItem = FindVisualParent<System.Windows.Controls.ListBoxItem>(e.OriginalSource as DependencyObject);
            object? item = listBoxItem?.DataContext ?? TemplatesList.SelectedItem;
            if (item is MessageTemplate template)
            {
                TemplatesList.SelectedItem = template;
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
                MessageBoxHelper.Show("El contenido de la plantilla no puede estar vacío.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newTemplate = new MessageTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Content = content,
                PautaId = _pautaId
            };

            _allTemplates.Add(newTemplate);
            LoadCategories();
            RefreshDisplay();

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
            _storageService.SaveTemplates(_allTemplates.ToList());
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            var selected = _allTemplates.Where(t => t.IsSelected).ToList();
            if (!selected.Any())
            {
                if (sender is FrameworkElement fe && fe.DataContext is MessageTemplate t)
                    selected.Add(t);
                else if (TemplatesList.SelectedItem is MessageTemplate st)
                    selected.Add(st);
                else return;
            }

            var orderedSelected = selected.OrderBy(t => _allTemplates.IndexOf(t)).ToList();
            foreach (var t in orderedSelected)
            {
                int idx = _allTemplates.IndexOf(t);
                if (idx > 0 && !_allTemplates[idx - 1].IsSelected)
                {
                    _allTemplates.Move(idx, idx - 1);
                }
            }
            RefreshDisplay();
            SaveCurrentState();
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            var selected = _allTemplates.Where(t => t.IsSelected).ToList();
            if (!selected.Any())
            {
                if (sender is FrameworkElement fe && fe.DataContext is MessageTemplate t)
                    selected.Add(t);
                else if (TemplatesList.SelectedItem is MessageTemplate st)
                    selected.Add(st);
                else return;
            }

            var orderedSelected = selected.OrderByDescending(t => _allTemplates.IndexOf(t)).ToList();
            foreach (var t in orderedSelected)
            {
                int idx = _allTemplates.IndexOf(t);
                if (idx < _allTemplates.Count - 1 && !_allTemplates[idx + 1].IsSelected)
                {
                    _allTemplates.Move(idx, idx + 1);
                }
            }
            RefreshDisplay();
            SaveCurrentState();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool all = _displayedTemplates.All(t => t.IsSelected);
            foreach (var t in _displayedTemplates) t.IsSelected = !all;
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _allTemplates.Where(t => t.IsSelected).ToList();
            if (selected.Any() && MessageBoxHelper.ShowNonCritical($"¿Eliminar {selected.Count} plantillas?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var t in selected) _allTemplates.Remove(t);
                LoadCategories();
                RefreshDisplay();
                SaveCurrentState();
            }
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            var vm = new ViewModels.TemplateManagementViewModel(_pautaId);
            var win = new TemplateManagementWindow { DataContext = vm, Owner = this };

            win.ShowDialog();

            // Recargar plantillas por si hubo cambios en la otra ventana
            _allTemplates.Clear();
            var updated = _storageService.LoadTemplatesForPauta(_pautaId);
            foreach (var t in updated) _allTemplates.Add(t);

            LoadCategories();
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            ApplyFilters();
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
                    int oldIdx = _allTemplates.IndexOf(dropped);
                    int newIdx = item != null ? _allTemplates.IndexOf((MessageTemplate)item.DataContext) : _allTemplates.Count - 1;
                    if (newIdx != -1 && oldIdx != newIdx)
                    {
                        _allTemplates.Move(oldIdx, newIdx);
                        RefreshDisplay();
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
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
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