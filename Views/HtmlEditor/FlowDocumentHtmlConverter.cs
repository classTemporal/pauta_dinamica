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

        /// <summary>Serializa una tabla con borde, encabezados y celdas.</summary>
        private static void SerializeTable(Table table, StringBuilder sb)
        {
            sb.Append("<table border=\"1\" cellpadding=\"4\" cellspacing=\"0\">");

            foreach (TableRowGroup group in table.RowGroups)
            {
                bool firstRow = group.Rows.Count > 0 && group.Rows[0] == table.RowGroups[0].Rows[0];

                foreach (TableRow row in group.Rows)
                {
                    sb.Append("<tr>");
                    bool header = firstRow && row == group.Rows[0];

                    foreach (TableCell cell in row.Cells)
                    {
                        sb.Append(header ? "<th>" : "<td>");
                        SerializeBlocks(cell.Blocks, sb);
                        sb.Append(header ? "</th>" : "</td>");
                    }

                    sb.Append("</tr>");
                }
            }

            sb.Append("</table>");
        }

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
                        bool colored = solid != null && solid.Color != Colors.Black;

                        if (colored && solid != null)
                        {
                            sb.Append("<span style=\"color:#")
                              .Append(solid.Color.R.ToString("X2"))
                              .Append(solid.Color.G.ToString("X2"))
                              .Append(solid.Color.B.ToString("X2"))
                              .Append("\">");
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
                        if (colored) sb.Append("</span>");
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

        private static string ImageWidthAttr(UIElement element)
        {
            if (element is System.Windows.Controls.Image img
                && !double.IsNaN(img.Width) && img.Width > 0)
                return $" width=\"{(int)img.Width}\" style=\"width:{(int)img.Width}px;height:auto;\"";
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
                TagCtx c = _ctx.Peek();

                if (c.Bold) run.FontWeight = FontWeights.Bold;
                if (c.Italic) run.FontStyle = FontStyles.Italic;
                if (c.Underline) run.TextDecorations = TextDecorations.Underline;
                if (c.Strike) run.TextDecorations = TextDecorations.Strikethrough;
                if (c.Color != null) run.Foreground = c.Color;

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
                    Color = prev.Color
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
            private void StartTable()
            {
                FlushPara();
                _table = new Table();
                _rowGroup = new TableRowGroup();
                _table.RowGroups.Add(_rowGroup);
                _root.Add(_table);
            }

            /// <summary>Cierra la tabla en curso descartando filas vacías.</summary>
            private void EndTable()
            {
                FlushPara();
                if (_table != null && _table.RowGroups.Count == 1 && _rowGroup!.Rows.Count == 0)
                    _root.Remove(_table);

                _table = null;
                _rowGroup = null;
                _row = null;
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
                        string? colorRef = GetAttr(m, "style") is string style && style.Length > 0
                            ? ParseColorFromStyle(style)
                            : GetAttr(m, "color");
                        var sc = Push(name);
                        if (!string.IsNullOrWhiteSpace(colorRef))
                            sc.Color = ParseColor(colorRef!, CurrentColor());
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
                        if (closing) EndTable(); else StartTable();
                        break;

                    case "tr":
                        if (_table != null && !closing)
                        {
                            _row = new TableRow();
                            _rowGroup!.Rows.Add(_row);
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
