using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;

namespace PautaDinamicaApp.Views
{
    public partial class TemplatePickerWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<MessageTemplate> _templates;
        private readonly StorageService _storageService;
        private bool _isMultiSelectMode;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
