using System;
using System.Windows;
using WControls = System.Windows.Controls;
using WDocs = System.Windows.Documents;
using WMedia = System.Windows.Media;

namespace PautaDinamicaApp.Views.HtmlEditor
{
    public partial class NativeRichEditor : System.Windows.Controls.UserControl, IHtmlEditor
    {
        private bool _syncing;
        private bool _dirty;
        private string _lastFlushed = "";

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
            var ed = (NativeRichEditor)d;
            string html = e.NewValue as string ?? "";
            // Rebote del binding tras un Flush propio: no reconstruir (perdería el cursor).
            if (html == ed._lastFlushed) return;
            ed.LoadHtml(html);
        }

        private void LoadHtml(string html)
        {
            if (_dirty) return;
            if (EditorBox == null) return;
            _syncing = true;
            try
            {
                var doc = FlowDocumentHtmlConverter.ToFlowDocument(html);
                NormalizeForDisplay(doc);
                EditorBox.Document = doc;
            }
            finally { _syncing = false; }
        }

        public void Flush()
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                string html = FlowDocumentHtmlConverter.ToHtml(EditorBox.Document);
                _lastFlushed = html;
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

        /// <inheritdoc/>
        public System.Threading.Tasks.Task FlushAsync()
        {
            Flush();
            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <inheritdoc/>
        public System.Threading.Tasks.Task InsertTextAtCaretAsync(string text)
        {
            InsertTextAtCaret(text);
            return System.Threading.Tasks.Task.CompletedTask;
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
            sel.ApplyPropertyValue(WDocs.TextElement.ForegroundProperty, ThemeDefaultBrush());
        }

        private void ColorBtn_Click(object s, RoutedEventArgs e)
        {
            string color = (s as WControls.Button)?.Tag as string ?? "auto";
            try
            {
                if (color == "auto")
                {
                    if (!EditorBox.Selection.IsEmpty)
                        EditorBox.Selection.ApplyPropertyValue(WDocs.TextElement.ForegroundProperty, ThemeDefaultBrush());
                    return;
                }
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
            var after = new WDocs.Paragraph(new WDocs.Run(""));
            table.SiblingBlocks.InsertAfter(table, after);
            EditorBox.CaretPosition = after.ContentStart;
            EditorBox.Focus();
            Flush();
        }

        private void FontShrink_Click(object s, RoutedEventArgs e) => WDocs.EditingCommands.DecreaseFontSize.Execute(null, EditorBox);
        private void FontGrow_Click(object s, RoutedEventArgs e) => WDocs.EditingCommands.IncreaseFontSize.Execute(null, EditorBox);

        private void EditorBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Enter al final de la última celda de la tabla = salir y escribir debajo,
            // en lugar de crear una fila nueva (Shift+Enter sigue siendo salto dentro).
            if (e.Key != System.Windows.Input.Key.Enter
                || (e.KeyboardDevice.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0)
                return;
            var (table, row, cell, idx) = TableAtCaret();
            if (table == null || row == null || cell == null) return;
            var group = row.Parent as WDocs.TableRowGroup;
            if (group == null) return;
            // En la última fila Enter nunca crea filas (solo +Fila lo hace):
            // sale de la tabla a un párrafo debajo. En el resto de filas, Enter
            // parte el párrafo con normalidad. Shift+Enter = salto dentro de celda.
            bool lastRow = group.Rows.IndexOf(row) == group.Rows.Count - 1;
            if (!lastRow) return;
            var after = new WDocs.Paragraph(new WDocs.Run(""));
            table.SiblingBlocks.InsertAfter(table, after);
            EditorBox.CaretPosition = after.ContentStart;
            EditorBox.Focus();
            e.Handled = true;
        }

        private void EditorThumb_DragDelta(object sender, WControls.Primitives.DragDeltaEventArgs e)
        {
            double h = EditorBox.Height + e.VerticalChange;
            if (double.IsNaN(h)) h = EditorBox.ActualHeight + e.VerticalChange;
            EditorBox.Height = Math.Min(900, Math.Max(250, h));
        }

        private static bool IsThemeDefault(WMedia.Color c) => IsNearBlack(c);

        private static bool IsDarkTheme()
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app?.Resources["BackgroundBrush"] is WMedia.SolidColorBrush bg)
                    return (0.299 * bg.Color.R + 0.587 * bg.Color.G + 0.114 * bg.Color.B) < 128;
                if (app?.Resources["TextBrush"] is WMedia.SolidColorBrush fg)
                    return (0.299 * fg.Color.R + 0.587 * fg.Color.G + 0.114 * fg.Color.B) > 128;
            }
            catch { }
            return false;
        }

        private static WMedia.Brush ThemeDefaultBrush()
            => new WMedia.SolidColorBrush(WMedia.Colors.Black);

        private static void NormalizeForDisplay(WDocs.FlowDocument doc)
        {
            // El editor es fondo blanco fijo (como el correo): el blanco explícito
            // heredado se pasa a negro para que no quede invisible.
            foreach (var (run, cellBg) in FindRuns(doc.Blocks))
            {
                if (run.Foreground is not WMedia.SolidColorBrush sb) continue;
                if (!IsNearWhite(sb.Color)) continue;
                bool bgDark = cellBg != null && IsDarkColor(cellBg.Value);
                if (!bgDark) run.Foreground = new WMedia.SolidColorBrush(WMedia.Colors.Black);
            }
        }

        private static bool IsDarkColor(WMedia.Color c)
            => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) < 128;
        private static bool IsNearBlack(WMedia.Color c) => c.R < 40 && c.G < 40 && c.B < 40;
        private static bool IsNearWhite(WMedia.Color c) => c.R > 215 && c.G > 215 && c.B > 215;

        private static System.Collections.Generic.IEnumerable<(WDocs.Run run, WMedia.Color? cellBg)> FindRuns(WDocs.BlockCollection blocks)
        {
            foreach (var b in blocks)
            {
                if (b is WDocs.Paragraph p)
                    foreach (var i in p.Inlines)
                    {
                        if (i is WDocs.Run r) yield return (r, null);
                    }
                else if (b is WDocs.Table t)
                    foreach (var g in t.RowGroups)
                        foreach (var r in g.Rows)
                            foreach (var c in r.Cells)
                            {
                                WMedia.Color? bg = (c.Background as WMedia.SolidColorBrush)?.Color;
                                foreach (var (run, _) in FindRuns(c.Blocks)) yield return (run, bg);
                            }
                else if (b is WDocs.Section s)
                    foreach (var r in FindRuns(s.Blocks)) yield return r;
                else if (b is WDocs.List l)
                    foreach (var li in l.ListItems)
                        foreach (var r in FindRuns(li.Blocks)) yield return r;
            }
        }

        private void ExitTable_Click(object s, RoutedEventArgs e)
        {
            var (table, _, _, _) = TableAtCaret();
            if (table == null) return;
            var after = new WDocs.Paragraph(new WDocs.Run(""));
            table.SiblingBlocks.InsertAfter(table, after);
            EditorBox.CaretPosition = after.ContentStart;
            EditorBox.Focus();
        }

        private void DeleteTable_Click(object s, RoutedEventArgs e)
        {
            var (table, _, _, _) = TableAtCaret();
            if (table == null) return;
            var parent = table.SiblingBlocks;
            var after = new WDocs.Paragraph(new WDocs.Run(""));
            parent.InsertAfter(table, after);
            parent.Remove(table);
            EditorBox.CaretPosition = after.ContentStart;
            EditorBox.Focus();
            Flush();
        }

        private void PickBrush(string title, Action<WMedia.Color> apply)
        {
            var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var sd = dlg.Color;
            apply(WMedia.Color.FromRgb(sd.R, sd.G, sd.B));
            Flush();
        }

        private void TextColorPicker_Click(object s, RoutedEventArgs e)
        {
            if (EditorBox.Selection.IsEmpty) return;
            PickBrush("Color de texto", c =>
            {
                // Negro en tema oscuro (o blanco en tema claro) = "volver al normal":
                // se usa el pincel del tema (al guardar se omite, sale negro en el correo).
                if (IsThemeDefault(c))
                    EditorBox.Selection.ApplyPropertyValue(WDocs.TextElement.ForegroundProperty, ThemeDefaultBrush());
                else
                    EditorBox.Selection.ApplyPropertyValue(WDocs.TextElement.ForegroundProperty, new WMedia.SolidColorBrush(c));
            });
        }

        private void CellBgPicker_Click(object s, RoutedEventArgs e)
        {
            var (_, _, cell, _) = TableAtCaret();
            if (cell == null) return;
            PickBrush("Fondo de celda", c => cell.Background = new WMedia.SolidColorBrush(c));
        }

        private void CellBgClear_Click(object s, RoutedEventArgs e)
        {
            var (_, _, cell, _) = TableAtCaret();
            if (cell == null) return;
            cell.ClearValue(WDocs.TableCell.BackgroundProperty);
            Flush();
        }

        private void AdjustCol(int delta)
        {
            var caret = EditorBox.CaretPosition;
            var (table, _, _, idx) = TableAtCaret();
            if (table == null || idx < 0) return;
            while (table.Columns.Count <= idx) table.Columns.Add(new WDocs.TableColumn());
            int cur = table.Columns[idx].Width.IsAbsolute ? (int)table.Columns[idx].Width.Value : 150;
            int next = Math.Min(600, Math.Max(40, cur + delta));
            table.Columns[idx].Width = new GridLength(next);
            Flush();
            try { EditorBox.CaretPosition = caret; } catch { }
            EditorBox.Focus();
        }

        private void AdjustRow(int delta)
        {
            var (_, row, _, _) = TableAtCaret();
            if (row == null) return;
            // En el mínimo (automático) seguir pulsando − no hace nada: no cicla.
            if (delta < 0 && row.Tag is not int) return;
            var caret = EditorBox.CaretPosition;
            int cur = row.Tag is int h ? h : 30;
            int next = Math.Min(400, Math.Max(0, cur + delta));
            row.Tag = next > 0 ? next : null;
            ApplyRowHeightVisual(row);
            Flush();
            try { EditorBox.CaretPosition = caret; } catch { }
            EditorBox.Focus();
        }

        internal static void ApplyRowHeightVisual(WDocs.TableRow row)
        {
            int h = row.Tag is int v ? v : 0;
            double vPad = h > 0 ? Math.Min(180, 4 + Math.Max(0, (h - 30) / 2.0)) : 4;
            foreach (var cell in row.Cells)
                cell.Padding = new Thickness(4, vPad, 4, vPad);
        }

        private void ColShrink_Click(object s, RoutedEventArgs e) => AdjustCol(-20);
        private void ColGrow_Click(object s, RoutedEventArgs e) => AdjustCol(20);
        private void RowShrink_Click(object s, RoutedEventArgs e) => AdjustRow(-10);
        private void RowGrow_Click(object s, RoutedEventArgs e) => AdjustRow(10);

        private (WDocs.Table? table, WDocs.TableRow? row, WDocs.TableCell? cell, int colIdx) TableAtCaret()
        {
            if (EditorBox == null) return (null, null, null, -1);
            var pos = EditorBox.CaretPosition;
            var cell = pos.Parent as WDocs.TableCell
                ?? (pos.Paragraph?.Parent as WDocs.TableCell);
            if (cell == null) return (null, null, null, -1);
            var row = cell.Parent as WDocs.TableRow;
            var group = row?.Parent as WDocs.TableRowGroup;
            var table = group?.Parent as WDocs.Table;
            int idx = row != null ? row.Cells.IndexOf(cell) : -1;
            return (table, row, cell, idx);
        }

        private void TableMenu_Opened(object s, RoutedEventArgs e)
        {
            var (table, _, _, _) = TableAtCaret();
            bool inTable = table != null;
            CellBgMenu.IsEnabled = inTable;
            ColWidthMenu.IsEnabled = inTable;
            RowHeightMenu.IsEnabled = inTable;
            AddRowMenu.IsEnabled = inTable;
            DelRowMenu.IsEnabled = inTable;
        }

        private void AddRow_Click(object s, RoutedEventArgs e)
        {
            var (table, row, _, _) = TableAtCaret();
            if (table == null || row == null) return;
            var group = row.Parent as WDocs.TableRowGroup;
            if (group == null) return;
            int idx = group.Rows.IndexOf(row);
            var nr = new WDocs.TableRow();
            for (int i = 0; i < row.Cells.Count; i++)
            {
                var nc = new WDocs.TableCell(new WDocs.Paragraph(new WDocs.Run("Texto")));
                nc.BorderBrush = new WMedia.SolidColorBrush(WMedia.Colors.Gray);
                nc.BorderThickness = new Thickness(1);
                nc.Padding = new Thickness(4);
                nr.Cells.Add(nc);
            }
            group.Rows.Insert(idx + 1, nr);
            Flush();
        }

        private void DelRow_Click(object s, RoutedEventArgs e)
        {
            var (_, row, _, _) = TableAtCaret();
            if (row == null) return;
            var group = row.Parent as WDocs.TableRowGroup;
            if (group == null || group.Rows.Count <= 1) return;
            group.Rows.Remove(row);
            Flush();
        }
    }
}
