using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using WControls = System.Windows.Controls;
using WForms = System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace PautaDinamicaApp.Views.HtmlEditor
{
    /// <summary>
    /// Editor de cuerpo HTML basado en WebView2 (Chromium contenteditable).
    /// El HTML que produce es el mismo que se guarda y se envía a Outlook, sin
    /// conversiones intermedias: lo que se pega (Word/Outlook) es lo que sale.
    /// </summary>
    public partial class WebView2HtmlEditor : WControls.UserControl, IHtmlEditor
    {
        private TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string _pendingHtml = "";
        private string _lastFlushed = "";
        private bool _syncing;

        public WebView2HtmlEditor()
        {
            InitializeComponent();
            FillFontCombos();
            FillTableBorderBox();
        }

        public string Html
        {
            get => (string)GetValue(HtmlProperty);
            set => SetValue(HtmlProperty, value);
        }

        public static readonly DependencyProperty HtmlProperty =
            DependencyProperty.Register(nameof(Html), typeof(string), typeof(WebView2HtmlEditor),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHtmlChanged));

        private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ed = (WebView2HtmlEditor)d;
            string html = e.NewValue as string ?? "";
            if (html == ed._lastFlushed) return; // rebote tras Flush propio
            ed._pendingHtml = html;
            _ = ed.ApplyPendingHtmlAsync();
        }

        #region Página embebida (sin archivos sueltos: funciona también en single-exe)

        private const string PageHtml = @"<!DOCTYPE html>
<html><head><meta charset=""utf-8"">
<style>
html,body{margin:0;padding:0;background:#fff;}
#ed{min-height:400px;padding:10px;font-family:'Segoe UI',Calibri,Arial,sans-serif;font-size:14px;color:#000;outline:none;word-wrap:break-word;}
#ed table{border-collapse:collapse;}
/* Tablas CON borde: como en el correo. Tablas SIN borde (border=0, ej. firmas):
   guía punteada tenue solo en el editor (no se guarda ni se envía). */
#ed table:not([border=""0""]) td,#ed table:not([border=""0""]) th{border:1px solid gray;padding:4px;}
#ed table[border=""0""] td,#ed table[border=""0""] th{border:1px dashed #bbbbbb;padding:4px;}
/* Tope absoluto (no porcentual): no interfiere con columnas angostas ni se serializa. */
#ed img{max-width:600px;height:auto;}
#ed p{margin:0 0 12px 0;}
/* 12px ≈ separación que Outlook aplica a los párrafos: lo que ves es lo que sale. */
</style></head>
<body><div id=""ed"" contenteditable=""true""></div>
<script>
const ed=()=>document.getElementById('ed');
let lastHtml='';
function notify(){const h=ed().innerHTML;if(h!==lastHtml){lastHtml=h;window.chrome.webview.postMessage({type:'change',html:h});}}
document.addEventListener('input',()=>notify());
document.addEventListener('click',(e)=>{const t=e.target&&e.target.closest?e.target.closest('img'):null;
ed().querySelectorAll('img[data-sel]').forEach(i=>i.removeAttribute('data-sel'));
if(t&&ed().contains(t))t.setAttribute('data-sel','1');});
// El clic en el toolbar WPF le quita el foco al documento y colapsa la selección:
// se guarda el último rango de TEXTO (no colapsado, para formato) y el último caret
// (incluso colapsado, para operaciones de tabla) y cada comando usa el suyo.
let savedRange=null,savedCaret=null,lastCaret='';
document.addEventListener('selectionchange',()=>{try{const s=getSelection();if(!s.rangeCount)return;
const r=s.getRangeAt(0);
try{savedCaret=r.cloneRange();}catch(e){}
if(!r.collapsed){try{savedRange=r.cloneRange();}catch(e){}}
const a=r.startContainer;const el=a.nodeType===1?a:a.parentElement;
if(el&&ed().contains(el)){const cs=getComputedStyle(el);
const fam=(cs.fontFamily||'').split(',')[0].replace(/['""]/g,'').trim();
const key=Math.round(parseFloat(cs.fontSize)||0)+'|'+fam;
if(key!==lastCaret){lastCaret=key;
try{window.chrome.webview.postMessage({type:'caret',size:Math.round(parseFloat(cs.fontSize)||0),family:fam});}catch(e){}}}}catch(e){}});
// Consulta bajo demanda (al abrir el combo o enfocarlo): reporta el formato bajo
// el caret/selección aunque la clave no haya cambiado.
function reportCaret(){try{const r=pickRange();if(!r)return;const a=r.startContainer;
const el=a.nodeType===1?a:a.parentElement;if(!el||!ed().contains(el))return;
const cs=getComputedStyle(el);
const fam=(cs.fontFamily||'').split(',')[0].replace(/['""]/g,'').trim();
const size=Math.round(parseFloat(cs.fontSize)||0);
lastCaret=size+'|'+fam;
try{window.chrome.webview.postMessage({type:'caret',size:size,family:fam});}catch(e){}}catch(e){}}
// Consulta síncrona (el combo la pide y usa el valor devuelto; no toca el foco).
function caretFormat(){try{let r=null;try{const s=getSelection();if(s.rangeCount)r=s.getRangeAt(0);}catch(e){}
if(!r){try{if(savedCaret&&savedCaret.startContainer.isConnected)r=savedCaret;}catch(e){}}
if(!r){try{if(savedRange&&savedRange.startContainer.isConnected)r=savedRange;}catch(e){}}
if(!r)return 'null';const a=r.startContainer;
const el=a.nodeType===1?a:a.parentElement;if(!el||!ed().contains(el))return 'null';
const cs=getComputedStyle(el);
const fam=(cs.fontFamily||'').split(',')[0].replace(/['""]/g,'').trim();
let tb=-1;try{tb=currentTableBorder();}catch(e){}
return JSON.stringify({size:Math.round(parseFloat(cs.fontSize)||0),family:fam,tableBorder:tb});}catch(e){return 'null';}}
function pickRange(){try{const s=getSelection();if(s.rangeCount)return s.getRangeAt(0);}catch(e){}
try{ed().focus();const s2=getSelection();if(s2.rangeCount)return s2.getRangeAt(0);}catch(e){}
try{if(savedCaret&&savedCaret.startContainer.isConnected)return savedCaret;}catch(e){}
try{if(savedRange&&savedRange.startContainer.isConnected)return savedRange;}catch(e){}
return null;}
// Rango solo para formato de texto: exige selección no colapsada.
function pickTextRange(){try{const s=getSelection();if(s.rangeCount&&!s.getRangeAt(0).collapsed)return s.getRangeAt(0);}catch(e){}
try{ed().focus();const s2=getSelection();if(s2.rangeCount&&!s2.getRangeAt(0).collapsed)return s2.getRangeAt(0);}catch(e){}
try{if(savedRange&&!savedRange.collapsed&&savedRange.startContainer.isConnected)return savedRange;}catch(e){}
return null;}
function anchorNode(){const r=pickRange();return r?r.startContainer:null;}
function cmd(n,v){ed().focus();try{document.execCommand(n,false,v==null?null:v);}catch(e){}notify();}
function setHtml(h){ed().innerHTML=h||'';lastHtml=ed().innerHTML;}
function getHtml(){ed().querySelectorAll('img[data-sel]').forEach(i=>i.removeAttribute('data-sel'));return ed().innerHTML;}
function insertText(t){ed().focus();try{if(!document.execCommand('insertText',false,t)){document.execCommand('insertHTML',false,escapeHtml(t));}}catch(e){document.execCommand('insertHTML',false,escapeHtml(t));}notify();}
function escapeHtml(s){return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}
function fontSizeDelta(d){const r=pickTextRange();let base=14;
if(r){const a=r.startContainer;const el=a.nodeType===1?a:a.parentElement;if(el)base=parseFloat(getComputedStyle(el).fontSize)||14;}
wrapSelectionStyle('fontSize',Math.min(200,Math.max(8,base+d))+'px');}
function wrapSelectionStyle(prop,val){const r=pickTextRange();if(!r)return;
const span=document.createElement('span');span.style[prop]=val;
try{r.surroundContents(span);}catch(e){try{span.appendChild(r.extractContents());r.insertNode(span);}catch(e2){return;}}
try{const s=getSelection();const nr=document.createRange();nr.selectNodeContents(span);s.removeAllRanges();s.addRange(nr);savedRange=nr.cloneRange();}catch(e){}
notify();}
function setFontFamily(n){if(n)wrapSelectionStyle('fontFamily',n);}
function setFontSizePx(px){if(px>0)wrapSelectionStyle('fontSize',px+'px');}
function tableAncestor(){const a=anchorNode();let n=a;while(n&&n!==ed()){if(n.tagName==='TABLE')return n;n=n.parentNode;}return null;}
function insertTable(r,c){let s='<table border=""1"" cellpadding=""4"" cellspacing=""0""><tbody>';
for(let i=0;i<r;i++){s+='<tr>';for(let j=0;j<c;j++)s+='<td>&nbsp;</td>';s+='</tr>';}s+='</tbody></table><p><br></p>';
ed().focus();document.execCommand('insertHTML',false,s);notify();}
function tableCellBorder(t){const bw=parseInt(t.getAttribute('data-bw')||'0',10);return bw>0?bw+'px solid #333':'';}
function addRow(){const t=tableAncestor();if(!t)return;const cols=t.rows[0]?t.rows[0].cells.length:1;
const cb=tableCellBorder(t);
const row=t.insertRow(-1);for(let j=0;j<cols;j++){const c=row.insertCell(-1);c.innerHTML='&nbsp;';if(cb)c.style.border=cb;}notify();}
function delRow(){const t=tableAncestor();if(!t)return;const a=anchorNode();let n=a;while(n&&n!==ed()){if(n.tagName==='TR'){n.remove();notify();return;}n=n.parentNode;}}
function exitTable(){const t=tableAncestor();if(!t)return;const p=document.createElement('p');p.innerHTML='<br>';t.parentNode.insertBefore(p,t.nextSibling);
const r=document.createRange();r.setStart(p,0);r.collapse(true);const s=getSelection();s.removeAllRanges();s.addRange(r);ed().focus();notify();}
function delTable(){const t=tableAncestor();if(t){t.remove();notify();}}
function setTableBorder(m){const t=tableAncestor();if(!t)return;applyTableBorder(t,m==='0'?0:1);notify();}
// Borde uniforme: misma línea en marco e interiores (el atributo border N solo
// engrosa el marco exterior y con relieve). En línea para que el correo lo vea igual.
function applyTableBorder(t,n){n=Math.max(0,Math.min(8,n|0));
if(n===0){t.setAttribute('border','0');t.style.border='';t.removeAttribute('data-bw');
for(const r of t.rows)for(const c of r.cells)c.style.border='';return;}
t.removeAttribute('border');t.setAttribute('data-bw',String(n));t.style.borderCollapse='collapse';
t.style.border=n+'px solid #333';
for(const r of t.rows)for(const c of r.cells)c.style.border=n+'px solid #333';}
function tableBorderWidth(d){const t=tableAncestor();if(!t)return '';
let n=parseInt(t.getAttribute('data-bw')||t.getAttribute('border')||'1',10);if(isNaN(n)||n<1)n=1;
applyTableBorder(t,Math.max(1,Math.min(8,n+d)));notify();return n;}
function colIndex(){const c=cellAncestor();return c?c.cellIndex:-1;}
function addCol(){const t=tableAncestor();if(!t)return;let idx=colIndex();if(idx<0)idx=t.rows[0]?t.rows[0].cells.length-1:0;
const cb=tableCellBorder(t);
for(const r of t.rows){try{const c=r.insertCell(idx+1);c.innerHTML='&nbsp;';if(cb)c.style.border=cb;}catch(e){}}notify();}
function delCol(){const t=tableAncestor();if(!t)return;const idx=colIndex();if(idx<0)return;
for(const r of t.rows){try{if(r.cells[idx])r.deleteCell(idx);}catch(e){}}
if(t.rows.length&&t.rows[0].cells.length===0)t.remove();notify();}
function currentTableBorder(){const t=tableAncestor();if(!t)return -1;return t.getAttribute('border')==='0'?0:1;}
function cellAncestor(){const a=anchorNode();let n=a;while(n&&n!==ed()){if(n.tagName==='TD'||n.tagName==='TH')return n;n=n.parentNode;}return null;}
function setCellBg(color){ed().focus();const c=cellAncestor();if(c){if(color)c.style.backgroundColor=color;else c.style.backgroundColor='';notify();}}
function insertImage(src,w){ed().focus();document.execCommand('insertHTML',false,'<img src=""'+src+'""'+(w?' width=""'+w+'"" style=""width:'+w+'px;height:auto;""':'')+'/>');notify();}
function imageResize(d){const im=ed().querySelector('img[data-sel]');if(!im)return '';
let w=im.width||im.naturalWidth||200;w=Math.max(32,w+d);im.setAttribute('width',w);im.style.width=w+'px';im.style.height='auto';notify();return w;}
function askLink(){const u=prompt('URL del enlace:','https://');if(u){ed().focus();document.execCommand('createLink',false,u);notify();}}
function askLineHeight(){const v=prompt('Interlineado (0.5 a 3, ej: 1.25):','1.25');if(!v)return;
const n=parseFloat(v.replace(',','.'));if(isNaN(n)||n<0.5||n>3){alert('Valor no válido. Usa un número entre 0.5 y 3.');return;}
setLineHeight(String(n));}
function colCells(){const a=anchorNode();let n=a;let td=null;while(n&&n!==ed()){if(n.tagName==='TD'||n.tagName==='TH')td=n;n=n.parentNode;}
if(!td)return[];const idx=td.cellIndex;let t=td.parentNode;while(t&&t.tagName!=='TABLE')t=t.parentNode;if(!t)return[];
const out=[];for(const r of t.rows){if(r.cells[idx])out.push(r.cells[idx]);}return out;}
function adjustCol(d){const cells=colCells();if(!cells.length)return;
cells.forEach(c=>{let w=c.style.width?parseFloat(c.style.width):(c.getAttribute('width')?parseFloat(c.getAttribute('width')):100);
w=Math.max(30,Math.min(900,w+d));c.style.width=w+'px';c.removeAttribute('width');});
notify();}
function adjustRow(d){const a=anchorNode();let n=a;let tr=null;while(n&&n!==ed()){if(n.tagName==='TR')tr=n;n=n.parentNode;}
if(!tr)return;for(const c of tr.cells){let h=c.style.height?parseFloat(c.style.height):c.offsetHeight;h=Math.max(16,Math.min(400,h+d));c.style.height=h+'px';}notify();}
function setLineHeight(v){const r=pickTextRange();if(!r)return;ed().focus();
const blocks=new Set();
const addBlock=(n)=>{while(n&&n!==ed()&&n){const t=n.tagName||'';if(/^(P|H1|H2|H3|H4|H5|H6|DIV|LI|BLOCKQUOTE)$/.test(t)){blocks.add(n);break;}n=n.parentNode;}};
addBlock(r.startContainer);addBlock(r.endContainer);
if(blocks.size===0){try{document.execCommand('formatBlock',false,'p');}catch(e){}addBlock(sel.anchorNode);}
blocks.forEach(b=>{b.style.lineHeight=v;b.style.marginTop='0';b.style.marginBottom='2px';});
notify();}
</script></body></html>";

        #endregion

        /// <summary>Inicializa el core y carga la página. True si quedó listo.</summary>
        public async Task<bool> InitAsync()
        {
            if (_ready.Task.IsCompleted) return await _ready.Task;
            try
            {
                // Carpeta de datos explícita dentro del AppData del USUARIO (sin admin):
                // el valor por defecto a veces falla en perfiles redirigidos o con
                // permisos raros, y era un falso "no disponible".
                System.Diagnostics.Debug.WriteLine("WebView2 init: creando entorno…");
                string dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PautaDinamica", "WebView2");
                Directory.CreateDirectory(dataDir);
                var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
                System.Diagnostics.Debug.WriteLine("WebView2 init: entorno listo, asegurando core…");
                await Web.EnsureCoreWebView2Async(env);
                System.Diagnostics.Debug.WriteLine("WebView2 init: core listo, cargando página…");
                Web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                Web.WebMessageReceived += Web_WebMessageReceived;
                // La página se sirve como archivo file:// (no NavigateToString): las
                // imágenes pegadas desde Word llegan como file:// locales y Chromium
                // solo las carga desde una página del mismo esquema. Sin esto se veían
                // rotas. El archivo se escribe en runtime (vale para single-exe).
                string pagePath = Path.Combine(dataDir, "editor.html");
                File.WriteAllText(pagePath, PageHtml);
                var navDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                void onNav(object s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    Web.NavigationCompleted -= onNav;
                    navDone.TrySetResult(e.IsSuccess);
                }
                Web.NavigationCompleted += onNav;
                Web.Source = new Uri(pagePath);
                var finished = await Task.WhenAny(navDone.Task, Task.Delay(TimeSpan.FromSeconds(10)));
                if (finished != navDone.Task)
                {
                    InitError = "el navegador no respondió al iniciar.";
                    _ready.TrySetResult(false);
                    return false;
                }
                if (!await navDone.Task)
                {
                    InitError = "no se pudo cargar la página del editor.";
                    _ready.TrySetResult(false);
                    return false;
                }
                System.Diagnostics.Debug.WriteLine("WebView2 init: página cargada, aplicando contenido…");
                await Web.CoreWebView2.ExecuteScriptAsync("document.execCommand('styleWithCSS', false, true);");
                bool ok = await ApplyHtmlToPageAsync(_pendingHtml ?? "");
                if (!ok) InitError = "no se pudo cargar el contenido inicial.";
                else System.Diagnostics.Debug.WriteLine("WebView2 init: listo.");
                _ready.TrySetResult(ok);
                return ok;
            }
            catch (Exception ex)
            {
                InitError = ex.Message;
                System.Diagnostics.Debug.WriteLine("WebView2 init falló: " + ex);
                _ready.TrySetResult(false);
                return false;
            }
        }

        /// <summary>Motivo del último fallo de inicialización (para el aviso de respaldo).</summary>
        public string InitError { get; private set; } = "";

        private bool _comboSync;

        private void Web_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var json = JsonDocument.Parse(e.WebMessageAsJson);
                if (json.RootElement.TryGetProperty("type", out var typeProp)
                    && typeProp.GetString() == "caret")
                {
                    int size = json.RootElement.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt32(out int s) ? s : 0;
                    string family = json.RootElement.TryGetProperty("family", out var famProp) ? famProp.GetString() ?? "" : "";
                    _ = Dispatcher.BeginInvoke(new Action(() => ShowCaretFormat(size, family)));
                    return;
                }
                if (!json.RootElement.TryGetProperty("html", out var htmlProp)) return;
                string html = htmlProp.GetString() ?? "";
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_syncing) return;
                    _syncing = true;
                    try
                    {
                        _lastFlushed = html;
                        if (html != Html) SetCurrentValue(HtmlProperty, html);
                    }
                    finally { _syncing = false; }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WebView2 mensaje inválido: " + ex.Message);
            }
        }

        /// <summary>Refleja en los combos la fuente/tamaño bajo el cursor (sin re-aplicar).</summary>
        private void ShowCaretFormat(int size, string family, int tableBorder = -1)
        {
            _comboSync = true;
            try
            {
                if (size > 0)
                {
                    string shown = size.ToString();
                    // El tamaño real puede no estar en la lista (p. ej. tras A±): se inserta ordenado.
                    if (!FontSizeBox.Items.Contains(shown))
                    {
                        int pos = 0;
                        while (pos < FontSizeBox.Items.Count
                               && int.TryParse(FontSizeBox.Items[pos]?.ToString(), out int v) && v < size)
                            pos++;
                        FontSizeBox.Items.Insert(pos, shown);
                    }
                    if (!Equals(FontSizeBox.SelectedItem, shown))
                        FontSizeBox.SelectedItem = shown;
                }
                if (!string.IsNullOrWhiteSpace(family) && FontFamilyBox.Items.Contains(family)
                    && !Equals(FontFamilyBox.SelectedItem, family))
                    FontFamilyBox.SelectedItem = family;
                // tableBorder: 1 = con borde, 0 = sin borde, -1 = fuera de tabla (se deja como está).
                if ((tableBorder == 0 || tableBorder == 1) && TableBorderBox.Items.Count > 0)
                {
                    string want = tableBorder == 0 ? "Sin borde" : "Con borde";
                    if (!Equals(TableBorderBox.SelectedItem, want))
                        TableBorderBox.SelectedItem = want;
                }
            }
            finally { _comboSync = false; }
        }

        private async Task<bool> ApplyPendingHtmlAsync()
        {
            if (!await _ready.Task) return false;
            return await ApplyHtmlToPageAsync(_pendingHtml ?? "");
        }

        /// <summary>Aplica HTML a la página sin exigir init completo (uso interno del init).</summary>
        private async Task<bool> ApplyHtmlToPageAsync(string html)
        {
            if (Web.CoreWebView2 == null) return false;
            try
            {
                await Web.CoreWebView2.ExecuteScriptAsync($"setHtml({JsonSerializer.Serialize(html)});");
                _lastFlushed = html;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WebView2 setHtml falló: " + ex.Message);
                return false;
            }
        }

        private async Task<string> GetHtmlFromPageAsync()
        {
            string raw = await Web.CoreWebView2.ExecuteScriptAsync("getHtml();");
            return JsonSerializer.Deserialize<string>(raw) ?? "";
        }

        /// <inheritdoc/>
        public async Task FlushAsync()
        {
            if (!await _ready.Task || Web.CoreWebView2 == null) return;
            try
            {
                // Con timeout: si el puente JS se atasca, se conserva el último Html
                // conocido (el binding en vivo lo mantiene al día) en vez de colgar
                // el guardado.
                var getHtml = GetHtmlFromPageAsync();
                bool finished = await Task.WhenAny(getHtml, Task.Delay(TimeSpan.FromSeconds(8))) == getHtml;
                string html = finished ? await getHtml : Html;
                html = InlineLocalImages(html);
                _syncing = true;
                try
                {
                    _lastFlushed = html;
                    if (html != Html) SetCurrentValue(HtmlProperty, html);
                }
                finally { _syncing = false; }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WebView2 flush falló: " + ex.Message);
            }
        }

        /// <inheritdoc/>
        public async Task InsertTextAtCaretAsync(string text)
        {
            if (string.IsNullOrEmpty(text) || !await _ready.Task || Web.CoreWebView2 == null) return;
            try
            {
                await Web.CoreWebView2.ExecuteScriptAsync($"insertText({JsonSerializer.Serialize(text)});");
                Web.Focus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WebView2 insertText falló: " + ex.Message);
            }
        }

        private void EditorThumb_DragDelta(object sender, WControls.Primitives.DragDeltaEventArgs e)
        {
            double h = EditorArea.Height + e.VerticalChange;
            if (double.IsNaN(h)) h = EditorArea.ActualHeight + e.VerticalChange;
            EditorArea.Height = Math.Min(900, Math.Max(250, h));
        }

        /// <summary>
        /// Las imágenes pegadas desde Word llegan como file:// locales que mueren al
        /// cerrar el equipo: se incrustan como data URI para que el correo sea autocontenido.
        /// </summary>
        private static string InlineLocalImages(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            if (html.IndexOf("file:", StringComparison.OrdinalIgnoreCase) < 0) return html;
            return Regex.Replace(html, @"<img\b[^>]*>", m =>
            {
                string tag = m.Value;
                var src = Regex.Match(tag, @"src\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (!src.Success || src.Groups[1].Value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    return tag;
                try
                {
                    string path = new Uri(src.Groups[1].Value).LocalPath;
                    if (!File.Exists(path)) return tag;
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    string mime = ext switch { ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".bmp" => "image/bmp", ".webp" => "image/webp", _ => "image/png" };
                    return Regex.Replace(tag, @"src\s*=\s*""[^""]+""",
                        $"src=\"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}\"",
                        RegexOptions.IgnoreCase);
                }
                catch { return tag; }
            }, RegexOptions.IgnoreCase);
        }

        #region Toolbar

        /// <summary>Llena los combos de fuente (sistema) y tamaños comunes.</summary>
        private void FillFontCombos()
        {
            if (FontFamilyBox.Items.Count > 0) return;
            try
            {
                FontFamilyBox.ItemsSource = System.Windows.Media.Fonts.SystemFontFamilies
                    .OrderBy(f => f.Source)
                    .Select(f => f.Source)
                    .ToList();
                FontFamilyBox.SelectedItem = "Segoe UI";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Fuentes del sistema no disponibles: " + ex.Message);
            }
            if (FontSizeBox.Items.Count == 0)
            {
                _comboSync = true;
                try
                {
                    foreach (int s in new[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72 })
                        FontSizeBox.Items.Add(s.ToString());
                    FontSizeBox.SelectedItem = "14";
                }
                finally { _comboSync = false; }
            }
        }

        private void FontFamilyBox_SelectionChanged(object sender, WControls.SelectionChangedEventArgs e)
        {
            if (_comboSync) return;
            if (FontFamilyBox.SelectedItem is string name && !string.IsNullOrWhiteSpace(name))
                RunJs($"setFontFamily({JsonSerializer.Serialize(name)});");
        }

        private void FontSizeBox_SelectionChanged(object sender, WControls.SelectionChangedEventArgs e)
        {
            if (_comboSync) return;
            ApplyFontSizeText(FontSizeBox.SelectedItem?.ToString() ?? FontSizeBox.Text);
        }

        /// <summary>Llena el combo de borde de tabla (solo-dropdown, sin re-aplicar al inicio).</summary>
        private void FillTableBorderBox()
        {
            if (TableBorderBox.Items.Count > 0) return;
            _comboSync = true;
            try
            {
                TableBorderBox.Items.Add("Con borde");
                TableBorderBox.Items.Add("Sin borde");
                TableBorderBox.SelectedItem = "Con borde";
            }
            finally { _comboSync = false; }
        }

        private void TableBorderBox_SelectionChanged(object sender, WControls.SelectionChangedEventArgs e)
        {
            if (_comboSync) return;
            if (Equals(TableBorderBox.SelectedItem, "Sin borde"))
                RunJs("setTableBorder('0');");
            else if (Equals(TableBorderBox.SelectedItem, "Con borde"))
                RunJs("setTableBorder('1');");
        }

        private void TableBorderBox_DropDownOpened(object sender, EventArgs e) => _ = RefreshCaretFormatAsync();

        /// <summary>
        /// Al abrir el combo o enfocarlo se consulta el formato bajo el cursor,
        /// porque el reporte automático se suprime si la clave no cambió.
        /// Se pide síncrono (valor devuelto) en vez de esperar el push por mensaje.
        /// </summary>
        private void FontSizeBox_DropDownOpened(object sender, EventArgs e) => _ = RefreshCaretFormatAsync();

        private void FontSizeBox_GotFocus(object sender, System.Windows.RoutedEventArgs e) => _ = RefreshCaretFormatAsync();

        private async System.Threading.Tasks.Task RefreshCaretFormatAsync()
        {
            if (!await _ready.Task || Web.CoreWebView2 == null) return;
            try
            {
                // ExecuteScriptAsync devuelve el valor JSON-serializado (string entrecomillado).
                string outer = await Web.CoreWebView2.ExecuteScriptAsync("caretFormat();");
                if (string.IsNullOrWhiteSpace(outer) || outer.Trim() == "null") return;
                string inner = System.Text.Json.JsonSerializer.Deserialize<string>(outer) ?? "";
                if (string.IsNullOrWhiteSpace(inner) || inner.Trim() == "null") return;
                using var json = JsonDocument.Parse(inner);
                int size = json.RootElement.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt32(out int s) ? s : 0;
                string family = json.RootElement.TryGetProperty("family", out var famProp) ? famProp.GetString() ?? "" : "";
                int tableBorder = json.RootElement.TryGetProperty("tableBorder", out var tbProp) && tbProp.TryGetInt32(out int tb) ? tb : -1;
                ShowCaretFormat(size, family, tableBorder);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Caret format falló: " + ex.Message); }
        }

        /// <summary>
        /// Elegir en el popup el mismo valor ya mostrado no dispara SelectionChanged:
        /// al cerrar se aplica lo visible.
        /// </summary>
        private void FontSizeBox_DropDownClosed(object sender, EventArgs e)
        {
            if (_comboSync) return;
            ApplyFontSizeText(FontSizeBox.Text);
        }

        private void FontSizeBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ApplyFontSizeText(FontSizeBox.Text);
                e.Handled = true;
            }
        }

        private void ApplyFontSizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (double.TryParse(text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double px)
                && px >= 6 && px <= 200)
            {
                RunJs($"setFontSizePx({px.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
                // Reflejar de inmediato lo aplicado (el reporte del caret lo confirma después).
                _comboSync = true;
                try
                {
                    string shown = px.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
                    if (!FontSizeBox.Items.Contains(shown))
                    {
                        int pos = 0;
                        while (pos < FontSizeBox.Items.Count
                               && int.TryParse(FontSizeBox.Items[pos]?.ToString(), out int v) && v < (int)px)
                            pos++;
                        FontSizeBox.Items.Insert(pos, shown);
                    }
                    if (!Equals(FontSizeBox.SelectedItem, shown)) FontSizeBox.SelectedItem = shown;
                }
                finally { _comboSync = false; }
            }
        }

        private async void RunJs(string js)
        {
            if (!await _ready.Task || Web.CoreWebView2 == null) return;
            try { await Web.CoreWebView2.ExecuteScriptAsync(js); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("WebView2 js falló: " + ex.Message); }
        }

        private void Bold_Click(object s, RoutedEventArgs e) => RunJs("cmd('bold');");
        private void Italic_Click(object s, RoutedEventArgs e) => RunJs("cmd('italic');");
        private void Underline_Click(object s, RoutedEventArgs e) => RunJs("cmd('underline');");
        private void FontDown_Click(object s, RoutedEventArgs e) => RunJs("fontSizeDelta(-2);");
        private void FontUp_Click(object s, RoutedEventArgs e) => RunJs("fontSizeDelta(2);");
        private void Bullets_Click(object s, RoutedEventArgs e) => RunJs("cmd('insertUnorderedList');");
        private void Numbers_Click(object s, RoutedEventArgs e) => RunJs("cmd('insertOrderedList');");
        private void Link_Click(object s, RoutedEventArgs e) => RunJs("askLink();");
        private void LineSingle_Click(object s, RoutedEventArgs e) => RunJs("setLineHeight('1');");
        private void LineMiddle_Click(object s, RoutedEventArgs e) => RunJs("setLineHeight('1.5');");
        private void LineDouble_Click(object s, RoutedEventArgs e) => RunJs("setLineHeight('2');");
        private void LineCustom_Click(object s, RoutedEventArgs e) => RunJs("askLineHeight();");
        private void ColShrink_Click(object s, RoutedEventArgs e) => RunJs("adjustCol(-20);");
        private void ColGrow_Click(object s, RoutedEventArgs e) => RunJs("adjustCol(20);");
        private void RowShrink_Click(object s, RoutedEventArgs e) => RunJs("adjustRow(-10);");
        private void RowGrow_Click(object s, RoutedEventArgs e) => RunJs("adjustRow(10);");
        private void ClearFormat_Click(object s, RoutedEventArgs e) => RunJs("cmd('removeFormat');");
        private void Table2x2_Click(object s, RoutedEventArgs e) => RunJs("insertTable(2,2);");
        private void Table3x2_Click(object s, RoutedEventArgs e) => RunJs("insertTable(3,2);");
        private void Table3x3_Click(object s, RoutedEventArgs e) => RunJs("insertTable(3,3);");
        private void AddRow_Click(object s, RoutedEventArgs e) => RunJs("addRow();");
        private void DelRow_Click(object s, RoutedEventArgs e) => RunJs("delRow();");
        private void AddCol_Click(object s, RoutedEventArgs e) => RunJs("addCol();");
        private void DelCol_Click(object s, RoutedEventArgs e) => RunJs("delCol();");
        private void BorderThin_Click(object s, RoutedEventArgs e) => RunJs("tableBorderWidth(-1);");
        private void BorderThick_Click(object s, RoutedEventArgs e) => RunJs("tableBorderWidth(1);");
        private void ExitTable_Click(object s, RoutedEventArgs e) => RunJs("exitTable();");
        private void DeleteTable_Click(object s, RoutedEventArgs e) => RunJs("delTable();");
        private void CellBgClear_Click(object s, RoutedEventArgs e) => RunJs("setCellBg(null);");
        private void ImageShrink_Click(object s, RoutedEventArgs e) => RunJs("imageResize(-40);");
        private void ImageGrow_Click(object s, RoutedEventArgs e) => RunJs("imageResize(40);");

        private void ForeColor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new WForms.ColorDialog { FullOpen = true };
            if (dlg.ShowDialog() == WForms.DialogResult.OK)
            {
                var c = dlg.Color;
                RunJs($"cmd('foreColor','#{c.R:X2}{c.G:X2}{c.B:X2}');");
            }
        }

        private void CellBg_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new WForms.ColorDialog { FullOpen = true };
            if (dlg.ShowDialog() == WForms.DialogResult.OK)
            {
                var c = dlg.Color;
                RunJs($"setCellBg('#{c.R:X2}{c.G:X2}{c.B:X2}');");
            }
        }

        private async void InsertImage_Click(object sender, RoutedEventArgs e)
        {
            if (!await _ready.Task || Web.CoreWebView2 == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Insertar imagen",
                Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(dlg.FileName);
                string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                string mime = ext switch { ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".bmp" => "image/bmp", ".webp" => "image/webp", _ => "image/png" };
                string uri = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                await Web.CoreWebView2.ExecuteScriptAsync($"insertImage({JsonSerializer.Serialize(uri)}, 0);");
                Web.Focus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Insertar imagen falló: " + ex.Message);
            }
        }

        #endregion
    }
}
