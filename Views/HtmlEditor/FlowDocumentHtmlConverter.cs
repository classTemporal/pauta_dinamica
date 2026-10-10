using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// El proyecto usa WPF y WinForms a la vez, así que Brush/Color/ColorConverter son
// ambiguos entre System.Drawing y System.Windows.Media. Se fija el de WPF aquí.
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace PautaDinamicaApp.Views.HtmlEditor
{
    /// <summary>
    /// Conversor entre HTML y <see cref="FlowDocument"/> de WPF, sin dependencias externas.
    ///
    /// <para>
    /// Existe para poder ofrecer un editor con formato (negritas, colores, listas y tablas)
    /// usando el <c>RichTextBox</c> nativo de WPF, que viene con .NET y funciona en
    /// cualquier equipo. La alternativa anterior dependía del runtime de Edge WebView2,
    /// que no está instalado en todas las máquinas.
    /// </para>
    /// <para>
    /// No pretende ser un navegador: cubre las etiquetas que genera el editor y las que
    /// Outlook entiende en el cuerpo de un correo.
    /// </para>
    /// </summary>
    public static class FlowDocumentHtmlConverter
    {
        /// <summary>Etiqueta HTML con su nombre y atributos.</summary>
        private static readonly Regex TagRegex = new(
            @"<(?<close>/)?(?<name>[a-zA-Z0-9!]+)(?<attrs>[^>]*?)(?<selfclose>/)?>",
            RegexOptions.Compiled);

        /// <summary>Atributo <c>nombre="valor"</c>, <c>nombre='valor'</c> o <c>nombre=valor</c>.</summary>
        private static readonly Regex AttrRegex = new(
            @"(?<name>[a-zA-Z-]+)\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bare>[^\s>]+))",
            RegexOptions.Compiled);

        /// <summary>Colores en notación hexadecimal de 3 o 6 dígitos.</summary>
        private static readonly Regex HexColorRegex = new(
            @"^#?([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$",
            RegexOptions.Compiled);

        /// <summary>
        /// Convierte HTML a un <see cref="FlowDocument"/> para mostrarlo en el editor.
        /// </summary>
        /// <param name="html">Cuerpo guardado. Si está vacío se devuelve un documento en blanco.</param>
        public static FlowDocument ToFlowDocument(string html)
        {
            var doc = new FlowDocument { PagePadding = new Thickness(0) };

            if (string.IsNullOrWhiteSpace(html))
                return doc;

            var builder = new BlockBuilder(doc);

            foreach (Match m in TagRegex.Matches(html))
            {
                // El texto previo a la etiqueta se procesó en la iteración anterior.
                if (m.Index > builder.Cursor)
                    builder.AppendText(html.Substring(builder.Cursor, m.Index - builder.Cursor));

                builder.Cursor = m.Index + m.Length;
                builder.Handle(m);
            }

            if (builder.Cursor < html.Length)
                builder.AppendText(html.Substring(builder.Cursor));

            builder.Finish();
            return doc;
        }

        /// <summary>
        /// Convierte el documento del editor a HTML para guardarlo y enviarlo a Outlook.
        /// </summary>
        /// <param name="doc">Documento a serializar. Si es nulo se devuelve cadena vacía.</param>
        public static string ToHtml(FlowDocument doc)
        {
            if (doc == null) return string.Empty;

            var sb = new StringBuilder();
            SerializeBlocks(doc.Blocks, sb);
            return sb.ToString();
        }

        /// <summary>Convierte una referencia de color HTML (<c>#rrggbb</c>, <c>red</c>) en brocha.</summary>
        internal static Brush ParseColor(string value, Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            string v = value.Trim();

            if (HexColorRegex.IsMatch(v))
            {
                if (v[0] == '#') v = v.Substring(1);
                if (v.Length == 3)
                    v = string.Concat(v[0], v[0], v[1], v[1], v[2], v[2]);

                try
                {
                    var color = Color.FromRgb(
                        Convert.ToByte(v.Substring(0, 2), 16),
                        Convert.ToByte(v.Substring(2, 2), 16),
                        Convert.ToByte(v.Substring(4, 2), 16));
                    return new SolidColorBrush(color);
                }
                catch (Exception)
                {
                    return fallback;
                }
            }

            try
            {
                var converted = ColorConverter.ConvertFromString(v);
                if (converted is Color c) return new SolidColorBrush(c);
            }
            catch (Exception)
            {
                // Color no reconocido: se conserva el anterior en lugar de fallar.
            }

            return fallback;
        }

        /// <summary>Decodifica las entidades HTML comunes; el resto se deja tal cual.</summary>
        internal static string DecodeEntities(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            return text
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&apos;", "'")
                .Replace("&nbsp;", "\u00A0")
                .Replace("&amp;", "&");
        }

        /// <summary>Escapa el texto para insertarlo en HTML sin romper el marcado.</summary>
        internal static string EncodeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
        /// <summary>Serializa una lista de bloques (párrafos, listas, tablas).</summary>
        private static void SerializeBlocks(IEnumerable<Block> blocks, StringBuilder sb)
        {
            foreach (Block block in blocks)
            {
                switch (block)
                {
                    case Paragraph p:
                        sb.Append("<p>");
                        SerializeInlines(p.Inlines, sb);
                        sb.Append("</p>");
                        break;

                    case List list:
                        string tag = list.MarkerStyle == TextMarkerStyle.Decimal ? "ol" : "ul";
                        sb.Append('<').Append(tag).Append('>');
                        foreach (ListItem item in list.ListItems)
                        {
                            sb.Append("<li>");
                            SerializeBlocks(item.Blocks, sb);
                            sb.Append("</li>");
                        }
                        sb.Append("</").Append(tag).Append('>');
                        break;

                    case Table table:
                        SerializeTable(table, sb);
                        break;

                    case Section section:
                        SerializeBlocks(section.Blocks, sb);
                        break;
                }
            }
        }

        /// <summary>Serializa una tabla con borde, anchos, alto de fila y fondos (compatibles con Outlook/Word).</summary>
        private static void SerializeTable(Table table, StringBuilder sb)
        {
            int totalW = TableTotalWidth(table);
            string tableBg = BrushToHex((table.Background as SolidColorBrush)?.Color);
            // El borde se preserva como está en el documento: las tablas pegadas sin
            // bordes (ej: firmas) no deben volver con bordes al guardar/recargar.
            bool bordered = TableHasVisibleBorders(table);
            sb.Append($"<table border=\"{(bordered ? "1" : "0")}\" cellpadding=\"4\" cellspacing=\"0\"");
            if (totalW > 0) sb.Append($" width=\"{totalW}\"");
            if (!string.IsNullOrEmpty(tableBg)) sb.Append($" bgcolor=\"{tableBg}\"");
            sb.Append(">");

            foreach (TableRowGroup group in table.RowGroups)
            {
                bool firstRow = group.Rows.Count > 0 && group.Rows[0] == table.RowGroups[0].Rows[0];

                foreach (TableRow row in group.Rows)
                {
                    int rowH = RowHeight(row);
                    sb.Append("<tr");
                    if (rowH > 0) sb.Append($" height=\"{rowH}\"");
                    string rowBg = BrushToHex((row.Background as SolidColorBrush)?.Color);
                    if (!string.IsNullOrEmpty(rowBg)) sb.Append($" bgcolor=\"{rowBg}\"");
                    sb.Append(">");
                    bool header = firstRow && row == group.Rows[0];

                    foreach (TableCell cell in row.Cells)
                    {
                        int colIdx = row.Cells.IndexOf(cell);
                        string cellW = ColumnWidthAttr(table, colIdx);
                        bool widthPx = !string.IsNullOrEmpty(cellW) && !cellW.EndsWith("*");
                        string cellBg = BrushToHex((cell.Background as SolidColorBrush)?.Color);
                        sb.Append(header ? "<th" : "<td");
                        if (!string.IsNullOrEmpty(cellW)) sb.Append($" width=\"{cellW}\"");
                        if (!string.IsNullOrEmpty(cellBg)) sb.Append($" bgcolor=\"{cellBg}\"");
                        if (widthPx || !string.IsNullOrEmpty(cellBg))
                        {
                            sb.Append(" style=\"");
                            if (widthPx) sb.Append($"width:{cellW}px;");
                            if (!string.IsNullOrEmpty(cellBg)) sb.Append($"background-color:{cellBg};");
                            sb.Append("\"");
                        }
                        sb.Append(">");
                        SerializeBlocks(cell.Blocks, sb);
                        sb.Append(header ? "</th>" : "</td>");
                    }

                    sb.Append("</tr>");
                }
            }

            sb.Append("</table>");
        }

        private static string? BrushToHex(Color? c)
            => c == null ? null : $"#{c.Value.R:X2}{c.Value.G:X2}{c.Value.B:X2}";

        /// <summary>Indica si la tabla muestra bordes (propios o en alguna celda).</summary>
        private static bool TableHasVisibleBorders(Table table)
        {
            if (IsThicknessVisible(table.BorderThickness)) return true;
            foreach (TableRowGroup group in table.RowGroups)
                foreach (TableRow row in group.Rows)
                    foreach (TableCell cell in row.Cells)
                        if (IsThicknessVisible(cell.BorderThickness)) return true;
            return false;
        }

        private static bool IsThicknessVisible(Thickness t)
            => t.Left > 0.01 || t.Top > 0.01 || t.Right > 0.01 || t.Bottom > 0.01;

        private static int TableTotalWidth(Table table)
        {
            int total = 0;
            foreach (var col in table.Columns)
                if (col.Width.IsAbsolute) total += (int)col.Width.Value;
            return total;
        }

        /// <summary>
        /// Ancho de columna para el HTML: píxeles ("200") si es absoluto,
        /// proporción ("2*") si es estrella, vacío si es automático.
        /// </summary>
        private static string ColumnWidthAttr(Table table, int idx)
        {
            if (idx < 0 || idx >= table.Columns.Count) return "";
            var w = table.Columns[idx].Width;
            if (w.IsAbsolute) return ((int)w.Value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (w.IsStar && w.Value > 0)
                return w.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "*";
            return "";
        }

        private static int RowHeight(TableRow row)
            => row.Tag is int h ? h : (row.Tag is string s && int.TryParse(s, out int v) ? v : 0);

        /// <summary>Serializa el contenido en línea aplicando negritas, colores y enlaces.</summary>
        private static void SerializeInlines(IEnumerable<Inline> inlines, StringBuilder sb)
        {
            foreach (Inline inline in inlines)
            {
                switch (inline)
                {
                    case Run run:
                        string text = EncodeText(run.Text);
                        if (text.Length == 0) break;

                        var solid = run.Foreground as SolidColorBrush;
                        bool colored = solid != null && !IsThemeDefault(solid.Color);

                        object localSize = run.ReadLocalValue(TextElement.FontSizeProperty);
                        bool sized = localSize is double fs && !double.IsNaN(fs) && fs > 0;

                        if ((colored && solid != null) || sized)
                        {
                            sb.Append("<span style=\"");
                            if (colored && solid != null)
                                sb.Append("color:#")
                                  .Append(solid.Color.R.ToString("X2"))
                                  .Append(solid.Color.G.ToString("X2"))
                                  .Append(solid.Color.B.ToString("X2"))
                                  .Append(';');
                            if (sized)
                                sb.Append("font-size:").Append(((double)localSize).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append("px;");
                            sb.Append("\">");
                        }

                        bool underlined = run.TextDecorations != null
                            && run.TextDecorations.Contains(TextDecorations.Underline[0]);

                        bool struck = run.TextDecorations != null
                            && run.TextDecorations.Contains(TextDecorations.Strikethrough[0]);

                        if (run.FontWeight == FontWeights.Bold) sb.Append("<b>");
                        if (run.FontStyle == FontStyles.Italic) sb.Append("<i>");
                        if (underlined) sb.Append("<u>");
                        if (struck) sb.Append("<strike>");

                        sb.Append(text);

                        if (struck) sb.Append("</strike>");
                        if (underlined) sb.Append("</u>");
                        if (run.FontStyle == FontStyles.Italic) sb.Append("</i>");
                        if (run.FontWeight == FontWeights.Bold) sb.Append("</b>");
                        if ((colored && solid != null) || sized) sb.Append("</span>");
                        break;

                    case Bold bold:
                        sb.Append("<b>");
                        SerializeInlines(bold.Inlines, sb);
                        sb.Append("</b>");
                        break;

                    case Italic italic:
                        sb.Append("<i>");
                        SerializeInlines(italic.Inlines, sb);
                        sb.Append("</i>");
                        break;

                    case Underline underline:
                        sb.Append("<u>");
                        SerializeInlines(underline.Inlines, sb);
                        sb.Append("</u>");
                        break;

                    case Hyperlink link:
                        if (link.NavigateUri != null)
                        {
                            sb.Append("<a href=\"")
                              .Append(EncodeText(link.NavigateUri.ToString()))
                              .Append("\">");
                        }
                        SerializeInlines(link.Inlines, sb);
                        if (link.NavigateUri != null) sb.Append("</a>");
                        break;

                    case Span span:
                        // Word/Outlook envuelven el texto pegado en Span (a veces con
                        // formato propio): sin este caso todo ese texto se perdía al
                        // guardar/enviar, quedando solo la estructura de las tablas.
                        // Va después de Bold/Italic/Underline/Hyperlink porque todos
                        // derivan de Span.
                        AppendSpanOpen(span, sb);
                        SerializeInlines(span.Inlines, sb);
                        AppendSpanClose(span, sb);
                        break;

                    case LineBreak:
                        sb.Append("<br/>");
                        break;

                    case InlineUIContainer container:
                        // Las imágenes se reincrustan como data URI base64 para que el
                        // correo sea autocontenido y Outlook no dependa de rutas locales.
                        string src = ImageToDataUri(container.Child);
                        if (!string.IsNullOrEmpty(src))
                        {
                            string widthAttr = ImageWidthAttr(container.Child);
                            sb.Append("<img src=\"").Append(src).Append("\"").Append(widthAttr).Append("/>");
                        }
                        break;
                }
            }
        }

        /// <summary>Abre las etiquetas de formato propio de un Span (misma regla que los Run).</summary>
        private static void AppendSpanOpen(Span span, StringBuilder sb)
        {
            var solid = span.Foreground as SolidColorBrush;
            bool colored = solid != null && !IsThemeDefault(solid.Color);

            object localSize = span.ReadLocalValue(TextElement.FontSizeProperty);
            bool sized = localSize is double fs && !double.IsNaN(fs) && fs > 0;

            if ((colored && solid != null) || sized)
            {
                sb.Append("<span style=\"");
                if (colored && solid != null)
                    sb.Append("color:#")
                      .Append(solid.Color.R.ToString("X2"))
                      .Append(solid.Color.G.ToString("X2"))
                      .Append(solid.Color.B.ToString("X2"))
                      .Append(';');
                if (sized)
                    sb.Append("font-size:").Append(((double)localSize).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append("px;");
                sb.Append("\">");
            }

            if (span.FontWeight == FontWeights.Bold) sb.Append("<b>");
            if (span.FontStyle == FontStyles.Italic) sb.Append("<i>");
            if (HasUnderline(span)) sb.Append("<u>");
            if (HasStrikethrough(span)) sb.Append("<strike>");
        }

        /// <summary>Cierra, en orden inverso, las etiquetas abiertas por <see cref="AppendSpanOpen"/>.</summary>
        private static void AppendSpanClose(Span span, StringBuilder sb)
        {
            var solid = span.Foreground as SolidColorBrush;
            bool colored = solid != null && !IsThemeDefault(solid.Color);
            object localSize = span.ReadLocalValue(TextElement.FontSizeProperty);
            bool sized = localSize is double fs && !double.IsNaN(fs) && fs > 0;

            if (HasStrikethrough(span)) sb.Append("</strike>");
            if (HasUnderline(span)) sb.Append("</u>");
            if (span.FontStyle == FontStyles.Italic) sb.Append("</i>");
            if (span.FontWeight == FontWeights.Bold) sb.Append("</b>");
            if ((colored && solid != null) || sized) sb.Append("</span>");
        }

        private static bool HasUnderline(Inline element)
            => element.TextDecorations != null
               && element.TextDecorations.Contains(TextDecorations.Underline[0]);

        private static bool HasStrikethrough(Inline element)
            => element.TextDecorations != null
               && element.TextDecorations.Contains(TextDecorations.Strikethrough[0]);

        private static bool IsThemeDefault(Color c)
        {
            bool black = c.R < 40 && c.G < 40 && c.B < 40;
            bool white = c.R > 215 && c.G > 215 && c.B > 215;
            return black || white;
        }

        private static string ImageWidthAttr(UIElement element)
        {
            if (element is System.Windows.Controls.Image img)
            {
                // Ancho explícito (imagen insertada con tamaño elegido).
                if (!double.IsNaN(img.Width) && img.Width > 0)
                    return $" width=\"{(int)img.Width}\" style=\"width:{(int)img.Width}px;height:auto;\"";
                // Sin ancho explícito (imagen pegada): usar su tamaño natural en DIPs
                // para que al recargar no se encoja ni la recorte una columna vecina.
                try
                {
                    if (img.Source is BitmapImage bmp && bmp.PixelWidth > 0 && bmp.Width > 0)
                    {
                        int w = (int)bmp.Width;
                        return $" width=\"{w}\" style=\"width:{w}px;height:auto;\"";
                    }
                }
                catch (Exception) { /* tamaño no disponible: sin atributo */ }
            }
            return string.Empty;
        }

        /// <summary>
        /// Convierte una imagen del editor a data URI PNG base64. Devuelve cadena vacía
        /// si el elemento no es una imagen o no se pudo leer su origen.
        /// </summary>
        private static string ImageToDataUri(UIElement element)
        {
            if (element is not System.Windows.Controls.Image img || img.Source is not BitmapImage bmp)
                return string.Empty;

            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));

                using var ms = new MemoryStream();
                encoder.Save(ms);
                return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
            }
            catch (Exception)
            {
                // Si la imagen no se puede re-codificar se omite en lugar de romper el cuerpo.
                return string.Empty;
            }
        }

        #region Parser HTML -> FlowDocument

        /// <summary>
        /// Estado de formato en línea (negrita, color, etc.) vigente para el texto que sigue.
        /// Se apila para poder restaurarlo al cerrar cada etiqueta.
        /// </summary>
        private sealed class TagCtx
        {
            public string Name = "";
            public bool Bold;
            public bool Italic;
            public bool Underline;
            public bool Strike;
            public Brush? Color;
            public double FontSize;
            public bool HasFontSize;
        }

        /// <summary>
        /// Construye el <see cref="FlowDocument"/> recorriendo el HTML de forma tolerante:
        /// las etiquetas desconocidas o sin cerrar no abortan el proceso, solo se ignoran.
        /// No es un parser HTML completo; cubre las etiquetas que genera el editor y las
        /// que Outlook entiende en el cuerpo de un correo.
        /// </summary>
        private sealed class BlockBuilder
        {
            private readonly FlowDocument _doc;
            private readonly Stack<TagCtx> _ctx = new();
            private readonly List<Block> _root = new();

            private Paragraph _para = new();
            private List? _list;
            private Table? _table;
            private TableRowGroup? _rowGroup;
            private TableRow? _row;
            private TableCell? _cell;
            // La tabla en curso muestra bordes (atributo border del <table>).
            // Ausente = con bordes, para no cambiar las plantillas ya guardadas.
            private bool _tableBordered = true;

            // Evita que un mismo párrafo se inserte dos veces (p.ej. el de un <li>,
            // que el handler ya colgó del ListItem al abrirlo).
            private bool _paraAttached;

            public BlockBuilder(FlowDocument doc) => _doc = doc;

            /// <summary>Posición leída del HTML de entrada.</summary>
            public int Cursor { get; set; }

            /// <summary>Añade texto (ya decodificado) al párrafo actual con el formato vigente.</summary>
            public void AppendText(string text)
            {
                string decoded = DecodeEntities(text);
                if (decoded.Length == 0) return;
                _para.Inlines.Add(BuildRun(decoded));
            }

            /// <summary>Crea un <see cref="Run"/> aplicando el formato del contexto actual.</summary>
            private Run BuildRun(string text)
            {
                var run = new Run(text);
                if (_ctx.Count == 0) return run;
                TagCtx c = _ctx.Peek();

                if (c.Bold) run.FontWeight = FontWeights.Bold;
                if (c.Italic) run.FontStyle = FontStyles.Italic;
                if (c.Underline) run.TextDecorations = TextDecorations.Underline;
                if (c.Strike) run.TextDecorations = TextDecorations.Strikethrough;
                if (c.Color != null) run.Foreground = c.Color;
                if (c.HasFontSize && c.FontSize > 0) run.FontSize = c.FontSize;

                return run;
            }

            /// <summary>Cierra el párrafo pendiente y vuelca los bloques raíz al documento.</summary>
            public void Finish()
            {
                FlushPara();
                foreach (Block b in _root) _doc.Blocks.Add(b);
            }

            /// <summary>
            /// Cierra el párrafo en curso colgándolo en su destino (celda, lista o raíz).
            /// Protege contra la doble inserción cuando el párrafo ya está adjunto (items &lt;li&gt;).
            /// </summary>
            private void FlushPara()
            {
                if (_paraAttached)
                {
                    _para = new Paragraph();
                    _paraAttached = false;
                    return;
                }

                if (_para.Inlines.Count > 0)
                {
                    if (_cell != null) _cell.Blocks.Add(_para);
                    else if (_list != null) _list.ListItems.Add(new ListItem(_para));
                    else _root.Add(_para);
                }

                _para = new Paragraph();
            }

            /// <summary>Aplica apertura/cierre de etiqueta de formato en línea sobre la pila.</summary>
            private void Toggle(string name, bool closing, Action<TagCtx> apply)
            {
                if (closing) { Pop(); return; }
                apply(Push(name));
            }

            /// <summary>Apila un contexto nuevo que hereda el formato del anterior.</summary>
            private TagCtx Push(string name)
            {
                TagCtx prev = _ctx.Count > 0 ? _ctx.Peek() : new TagCtx();
                var ctx = new TagCtx
                {
                    Name = name,
                    Bold = prev.Bold,
                    Italic = prev.Italic,
                    Underline = prev.Underline,
                    Strike = prev.Strike,
                    Color = prev.Color,
                    FontSize = prev.FontSize,
                    HasFontSize = prev.HasFontSize
                };
                _ctx.Push(ctx);
                return ctx;
            }

            /// <summary>Desapila un contexto, sin eliminar la base.</summary>
            private void Pop()
            {
                if (_ctx.Count > 1) _ctx.Pop();
            }

            /// <summary>Color vigente del contexto más interno (o negro por defecto).</summary>
            private Brush CurrentColor()
                => _ctx.Count > 0 ? _ctx.Peek().Color ?? System.Windows.Media.Brushes.Black
                                  : System.Windows.Media.Brushes.Black;

            // BUILDER_PART2

            /// <summary>Abre una lista y la añade a la raíz del documento.</summary>
            private void StartList(TextMarkerStyle marker)
            {
                FlushPara();
                _list = new List { MarkerStyle = marker };
                _root.Add(_list);
            }

            /// <summary>Cierra la lista en curso.</summary>
            private void EndList()
            {
                FlushPara();
                _list = null;
            }

            /// <summary>Abre una tabla y la añade a la raíz del documento.</summary>
            private void StartTable(Match m)
            {
                FlushPara();
                _table = new Table();
                _table.CellSpacing = 0;
                string? border = GetAttr(m, "border");
                _tableBordered = string.IsNullOrWhiteSpace(border) || border.Trim() != "0";
                string? bg = GetAttr(m, "bgcolor") ?? ParseBgFromStyle(GetAttr(m, "style") ?? "");
                if (!string.IsNullOrWhiteSpace(bg))
                    _table.Background = ParseColor(bg, null);
                string? w = GetAttr(m, "width");
                if (!string.IsNullOrWhiteSpace(w) && int.TryParse(w.Trim().TrimEnd('p', 'x'), out int tw) && tw > 0)
                    _table.Tag = tw;
                _rowGroup = new TableRowGroup();
                _table.RowGroups.Add(_rowGroup);
                _root.Add(_table);
            }

            /// <summary>Cierra la tabla en curso descartando filas vacías.</summary>
            private void EndTable()
            {
                FlushPara();
                if (_table != null)
                {
                    // Aplica el alto visual de las filas cargadas desde el HTML.
                    foreach (var g in _table.RowGroups)
                        foreach (var r in g.Rows)
                            NativeRichEditor.ApplyRowHeightVisual(r);
                    if (_table.RowGroups.Count == 1 && _rowGroup!.Rows.Count == 0)
                        _root.Remove(_table);
                }

                _table = null;
                _rowGroup = null;
                _row = null;
                _tableBordered = true;
            }

            /// <summary>Lee un atributo por nombre (sin distinguir mayúsculas) de una etiqueta.</summary>
            private static string? GetAttr(Match m, string name)
            {
                foreach (Match a in AttrRegex.Matches(m.Groups["attrs"].Value))
                {
                    if (!string.Equals(a.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return a.Groups["dq"].Success ? a.Groups["dq"].Value
                         : a.Groups["sq"].Success ? a.Groups["sq"].Value
                         : a.Groups["bare"].Value;
                }

                return null;
            }

            /// <summary>Extrae <c>color:#rrggbb</c> de un atributo <c>style</c>, si existe.</summary>
            private static string? ParseColorFromStyle(string style)
            {
                var m = Regex.Match(style, @"(?:^|;)\s*color\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }

            private static string? ParseBgFromStyle(string style)
            {
                var m = Regex.Match(style, @"(?:^|;)\s*background(?:-color)?\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }

            /// <summary>
            /// Lee un ancho de columna/celda del HTML: píxeles ("200", "200px") o
            /// proporción ("2*", formato propio para no perder columnas estrella).
            /// Null si está vacío o no se reconoce.
            /// </summary>
            private static GridLength? ParseColumnWidth(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return null;
                string v = value.Trim();
                if (v.EndsWith("*"))
                {
                    string num = v.Substring(0, v.Length - 1).Trim();
                    if (double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double star) && star > 0)
                        return new GridLength(star, GridUnitType.Star);
                    return null;
                }
                if (int.TryParse(v.TrimEnd('p', 'x'), out int px) && px > 0)
                    return new GridLength(px);
                return null;
            }

            private static double ParseFontSize(string? style)
            {
                if (string.IsNullOrEmpty(style)) return 0;
                var m = Regex.Match(style, @"(?:^|;)\s*font-size\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                if (!m.Success) return 0;
                string v = m.Groups[1].Value.Trim().ToLowerInvariant();
                double factor = 1;
                if (v.EndsWith("pt")) { factor = 96.0 / 72.0; v = v.Substring(0, v.Length - 2); }
                else if (v.EndsWith("px")) v = v.Substring(0, v.Length - 2);
                if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double n) && n > 0)
                    return Math.Min(200, n * factor);
                return 0;
            }

            /// <summary>
            /// Inserta una imagen en el párrafo actual. Admite <c>data:</c> (base64, que es lo
            /// que guarda el editor) y rutas/URL normales; si no puede cargarla, la ignora.
            /// </summary>
            private void TryAddImage(Match m)
            {
                string? src = GetAttr(m, "src");
                if (string.IsNullOrWhiteSpace(src)) return;

                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;

                    if (src.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                    {
                        int comma = src.IndexOf(',');
                        if (comma < 0) return;
                        byte[] bytes = Convert.FromBase64String(src.Substring(comma + 1));
                        bitmap.StreamSource = new MemoryStream(bytes);
                    }
                    else
                    {
                        bitmap.UriSource = new Uri(src, UriKind.RelativeOrAbsolute);
                    }

                    bitmap.EndInit();

                    var image = new System.Windows.Controls.Image
                    {
                        Source = bitmap,
                        Margin = new Thickness(2)
                    };
                    string? w = GetAttr(m, "width");
                    if (!string.IsNullOrWhiteSpace(w) && int.TryParse(w.Trim().TrimEnd('p', 'x'), out int iw) && iw > 0)
                    { image.Width = iw; image.Stretch = System.Windows.Media.Stretch.Uniform; }
                    else image.Stretch = System.Windows.Media.Stretch.None;

                    _para.Inlines.Add(new InlineUIContainer(image));
                }
                catch (Exception)
                {
                    // Imagen inválida o no soportada: se omite en lugar de romper el documento.
                }
            }

            /// <summary>
            /// Procesa una etiqueta del HTML. <paramref name="m"/> trae el nombre,
            /// si es de cierre y sus atributos.
            /// </summary>
            public void Handle(Match m)
            {
                string name = m.Groups["name"].Value.ToLowerInvariant();
                bool closing = m.Groups["close"].Success;

                switch (name)
                {
                    case "p":
                    case "div":
                    case "h1": case "h2": case "h3":
                    case "h4": case "h5": case "h6":
                    case "blockquote":
                    case "pre":
                        if (closing) FlushPara();
                        break;

                    case "br":
                        _para.Inlines.Add(new LineBreak());
                        break;

                    case "b": case "strong":
                        Toggle(name, closing, c => c.Bold = !closing);
                        break;
                    case "i": case "em":
                        Toggle(name, closing, c => c.Italic = !closing);
                        break;
                    case "u":
                        Toggle(name, closing, c => c.Underline = !closing);
                        break;
                    case "s": case "strike": case "del":
                        Toggle(name, closing, c => c.Strike = !closing);
                        break;

                    case "span":
                    case "font":
                        if (closing) { Pop(); break; }
                        string? styleAttr = GetAttr(m, "style");
                        string? colorRef = !string.IsNullOrEmpty(styleAttr)
                            ? ParseColorFromStyle(styleAttr)
                            : GetAttr(m, "color");
                        var sc = Push(name);
                        if (!string.IsNullOrWhiteSpace(colorRef))
                            sc.Color = ParseColor(colorRef!, CurrentColor());
                        double fsize = ParseFontSize(styleAttr);
                        if (fsize > 0) { sc.FontSize = fsize; sc.HasFontSize = true; }
                        break;

                    case "a":
                        if (closing) Pop(); else Push(name);
                        break;

                    case "ul":
                    case "ol":
                        if (closing) EndList();
                        else StartList(name == "ol" ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc);
                        break;

                    case "li":
                        if (!closing && _list != null)
                        {
                            FlushPara();
                            _para = new Paragraph();
                            _list.ListItems.Add(new ListItem(_para));
                        }
                        break;

                    case "table":
                        if (closing) EndTable(); else StartTable(m);
                        break;

                    case "tr":
                        if (_table != null && !closing)
                        {
                            _row = new TableRow();
                            string? rh = GetAttr(m, "height");
                            if (!string.IsNullOrWhiteSpace(rh) && int.TryParse(rh.Trim().TrimEnd('p', 'x'), out int hh) && hh > 0)
                                _row.Tag = hh;
                            string? rbg = GetAttr(m, "bgcolor") ?? ParseBgFromStyle(GetAttr(m, "style") ?? "");
                            if (!string.IsNullOrWhiteSpace(rbg))
                                _row.Background = ParseColor(rbg, null);
                            _rowGroup!.Rows.Add(_row);
                        }
                        break;

                    case "col":
                        if (_table != null && !closing)
                        {
                            GridLength? colWidth = ParseColumnWidth(GetAttr(m, "width"));
                            if (colWidth != null)
                                _table.Columns.Add(new TableColumn { Width = colWidth.Value });
                        }
                        break;

                    case "td":
                    case "th":
                        if (_row != null)
                        {
                            if (closing)
                            {
                                FlushPara();
                                if (_cell!.Blocks.Count == 0) _cell.Blocks.Add(new Paragraph());
                                _cell = null;
                            }
                            else
                            {
                                _cell = new TableCell();
                                // Los bordes solo si la tabla los trae (border="1" o ausente);
                                // una tabla pegada sin bordes debe seguir sin bordes.
                                if (_tableBordered)
                                {
                                    _cell.BorderBrush = System.Windows.Media.Brushes.Gray;
                                    _cell.BorderThickness = new Thickness(1);
                                }
                                _cell.Padding = new Thickness(4);
                                string? cbg = GetAttr(m, "bgcolor") ?? ParseBgFromStyle(GetAttr(m, "style") ?? "");
                                if (!string.IsNullOrWhiteSpace(cbg))
                                    _cell.Background = ParseColor(cbg, null);
                                GridLength? cellWidth = ParseColumnWidth(GetAttr(m, "width"));
                                if (cellWidth != null && _table != null)
                                {
                                    int idx = _row.Cells.Count;
                                    while (_table.Columns.Count <= idx)
                                        _table.Columns.Add(new TableColumn());
                                    _table.Columns[idx].Width = cellWidth.Value;
                                }
                                _row.Cells.Add(_cell);
                                _para = new Paragraph();
                            }
                        }
                        break;

                    case "img":
                        TryAddImage(m);
                        break;

                    default:
                        // Etiqueta desconocida o de cierre sin apertura: se ignora.
                        break;
                }
            }
        }

        #endregion
    }
}
