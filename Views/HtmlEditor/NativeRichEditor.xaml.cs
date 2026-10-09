using System;
using System.Windows;
using WControls = System.Windows.Controls;
using WDocs = System.Windows.Documents;
using WMedia = System.Windows.Media;

namespace PautaDinamicaApp.Views.HtmlEditor
{
    public partial class NativeRichEditor : System.Windows.Controls.UserControl
    {
        private bool _syncing;
        private bool _dirty;

        public NativeRichEditor() => InitializeComponent();

        public string Html
        {
            get => (string)GetValue(HtmlProperty);
            set => SetValue(HtmlProperty, value);
        }

        public static readonly DependencyProperty HtmlProperty =
            DependencyProperty.Register(nameof(Html), typeof(string), typeof(NativeRichEditor),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHtmlChanged));

        private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((NativeRichEditor)d).LoadHtml(e.NewValue as string ?? "");
        }

        private void LoadHtml(string html)
        {
            if (_dirty) return;
            if (EditorBox == null) return;
            _syncing = true;
            try { EditorBox.Document = FlowDocumentHtmlConverter.ToFlowDocument(html); }
            finally { _syncing = false; }
        }

        public void Flush()
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                string html = FlowDocumentHtmlConverter.ToHtml(EditorBox.Document);
                if (html != Html) SetCurrentValue(HtmlProperty, html);
                _dirty = false;
            }
            finally { _syncing = false; }
        }

        public void InsertTextAtCaret(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            EditorBox.CaretPosition.InsertTextInRun(text);
            EditorBox.Focus();
            Flush();
        }

        private void EditorBox_TextChanged(object s, WControls.TextChangedEventArgs e)
        {
            if (_syncing) return;
            _dirty = true;
        }

        private void EditorBox_LostFocus(object s, RoutedEventArgs e) => Flush();

        private void BoldBtn_Click(object s, RoutedEventArgs e) => WDocs.EditingCommands.ToggleBold.Execute(null, EditorBox);
        private void ItalicBtn_Click(object s, RoutedEventArgs e) => WDocs.EditingCommands.ToggleItalic.Execute(null, EditorBox);
        private void UnderlineBtn_Click(object s, RoutedEventArgs e) => WDocs.EditingCommands.ToggleUnderline.Execute(null, EditorBox);
        private void BulletsBtn_Click(object s, RoutedEventArgs e) => WDocs.EditingCommands.ToggleBullets.Execute(null, EditorBox);
        private void NumbersBtn_Click(object s, RoutedEventArgs e) => WDocs.EditingCommands.ToggleNumbering.Execute(null, EditorBox);
        private void ClearBtn_Click(object s, RoutedEventArgs e)
        {
            var sel = EditorBox.Selection;
            if (sel.IsEmpty) return;
            sel.ApplyPropertyValue(WDocs.TextElement.FontWeightProperty, FontWeights.Normal);
            sel.ApplyPropertyValue(WDocs.TextElement.FontStyleProperty, FontStyles.Normal);
            sel.ApplyPropertyValue(WDocs.Inline.TextDecorationsProperty, null);
            sel.ApplyPropertyValue(WDocs.TextElement.ForegroundProperty, new WMedia.SolidColorBrush(WMedia.Colors.Black));
        }

        private void ColorBtn_Click(object s, RoutedEventArgs e)
        {
            string color = (s as WControls.Button)?.Tag as string ?? "#000000";
            try
            {
                var c = (WMedia.Color)WMedia.ColorConverter.ConvertFromString(color);
                if (!EditorBox.Selection.IsEmpty)
                    EditorBox.Selection.ApplyPropertyValue(WDocs.TextElement.ForegroundProperty, new WMedia.SolidColorBrush(c));
            }
            catch { }
        }

        private void LinkBtn_Click(object s, RoutedEventArgs e)
        {
            var box = new WControls.TextBox { Height = 32, VerticalContentAlignment = VerticalAlignment.Center, Text = "https://" };
            var win = new Window
            {
                Title = "Insertar enlace", Width = 420, Height = 175,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this), WindowStyle = WindowStyle.ToolWindow
            };
            var panel = new WControls.StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new WControls.TextBlock { Text = "Dirección del enlace:", Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(box);
            var row = new WControls.StackPanel { Orientation = WControls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new WControls.Button { Content = "Aceptar", Width = 95, Height = 32, IsDefault = true };
            ok.Click += (_, __) => win.DialogResult = true;
            row.Children.Add(ok);
            row.Children.Add(new WControls.Button { Content = "Cancelar", Width = 95, Height = 32, IsCancel = true });
            panel.Children.Add(row);
            win.Content = panel;
            if (win.ShowDialog() != true || string.IsNullOrWhiteSpace(box.Text)) return;
            string url = box.Text.Trim();
            string selText = EditorBox.Selection.IsEmpty ? url : EditorBox.Selection.Text.Trim();
            if (string.IsNullOrEmpty(selText)) selText = url;
            var link = new WDocs.Hyperlink(new WDocs.Run(selText));
            try { link.NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute); } catch { return; }
            if (!EditorBox.Selection.IsEmpty) EditorBox.Selection.Text = "";
            var para = EditorBox.CaretPosition.Paragraph;
            if (para != null) para.Inlines.Add(link);
            else EditorBox.Document.Blocks.Add(new WDocs.Paragraph(link));
            Flush();
        }

        private void ImageBtn_Click(object s, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.bmp" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var bmp = new WMedia.Imaging.BitmapImage(new Uri(dlg.FileName));
                var img = new WControls.Image { Source = bmp, Margin = new Thickness(2) };
                ApplyImageSize(img);
                var para = EditorBox.CaretPosition.Paragraph;
                var container = new WDocs.InlineUIContainer(img);
                if (para != null) para.Inlines.Add(container);
                Flush();
            }
            catch { }
        }

        private int SelectedImageSize()
        {
            if (ImageSizeBox?.SelectedItem is WControls.ComboBoxItem item
                && int.TryParse(item.Tag?.ToString(), out int w)) return w;
            return 0;
        }

        private void ApplyImageSize(WControls.Image img)
        {
            int w = SelectedImageSize();
            if (w > 0) { img.Width = w; img.Stretch = WMedia.Stretch.Uniform; }
            else { img.ClearValue(WControls.Image.WidthProperty); img.Stretch = WMedia.Stretch.None; }
        }

        private void ImageSizeBox_SelectionChanged(object s, WControls.SelectionChangedEventArgs e)
        {
            if (EditorBox == null) return;
            var img = FindImageAtCaret();
            if (img != null) { ApplyImageSize(img); Flush(); }
        }

        private WControls.Image? FindImageAtCaret()
        {
            if (EditorBox == null) return null;
            var pos = EditorBox.CaretPosition;
            if (pos.Paragraph == null) return null;
            foreach (var inl in pos.Paragraph.Inlines)
                if (inl is WDocs.InlineUIContainer c && c.Child is WControls.Image img) return img;
            if (EditorBox.Selection.Start.Paragraph != null)
                foreach (var inl in EditorBox.Selection.Start.Paragraph.Inlines)
                    if (inl is WDocs.InlineUIContainer c && c.Child is WControls.Image img) return img;
            return null;
        }

        private void ScaleImageAtCaret(double factor)
        {
            var img = FindImageAtCaret();
            if (img == null) return;
            double cur = double.IsNaN(img.Width) || img.Width <= 0
                ? (img.Source is WMedia.Imaging.BitmapSource bs ? bs.PixelWidth : 400) : img.Width;
            double next = Math.Min(1200, Math.Max(50, cur * factor));
            img.Width = next;
            img.Stretch = WMedia.Stretch.Uniform;
            SyncSizeBox((int)next);
            Flush();
        }

        private void SyncSizeBox(int w)
        {
            if (ImageSizeBox == null) return;
            foreach (var it in ImageSizeBox.Items)
                if (it is WControls.ComboBoxItem ci && ci.Tag?.ToString() == w.ToString())
                { ImageSizeBox.SelectedItem = it; return; }
        }

        private void ImageShrinkBtn_Click(object s, RoutedEventArgs e) => ScaleImageAtCaret(0.85);
        private void ImageGrowBtn_Click(object s, RoutedEventArgs e) => ScaleImageAtCaret(1.18);

        private void TableBtn_Click(object s, RoutedEventArgs e)
        {
            var parts = ((s as WControls.Button)?.Tag as string ?? "2,2").Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int cols) || !int.TryParse(parts[1], out int rows)) return;
            var table = new WDocs.Table();
            table.CellSpacing = 0;
            table.BorderBrush = new WMedia.SolidColorBrush(WMedia.Colors.Gray);
            table.BorderThickness = new Thickness(1);
            for (int c = 0; c < cols; c++) table.Columns.Add(new WDocs.TableColumn());
            var group = new WDocs.TableRowGroup();
            table.RowGroups.Add(group);
            for (int r = 0; r < rows; r++)
            {
                var row = new WDocs.TableRow();
                group.Rows.Add(row);
                for (int c = 0; c < cols; c++)
                {
                    var cell = new WDocs.TableCell(new WDocs.Paragraph(new WDocs.Run(r == 0 ? $"Encabezado {c + 1}" : "Texto")));
                    cell.BorderBrush = new WMedia.SolidColorBrush(WMedia.Colors.Gray);
                    cell.BorderThickness = new Thickness(1);
                    cell.Padding = new Thickness(4);
                    row.Cells.Add(cell);
                }
            }
            var pos = EditorBox.CaretPosition;
            if (pos.Paragraph != null) pos.Paragraph.SiblingBlocks.InsertAfter(pos.Paragraph, table);
            else EditorBox.Document.Blocks.Add(table);
            Flush();
        }

        private void EditorThumb_DragDelta(object sender, WControls.Primitives.DragDeltaEventArgs e)
        {
            double h = EditorBox.Height + e.VerticalChange;
            if (double.IsNaN(h)) h = EditorBox.ActualHeight + e.VerticalChange;
            EditorBox.Height = Math.Min(900, Math.Max(250, h));
        }
    }
}
