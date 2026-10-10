using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PautaDinamicaApp.Views.HtmlEditor
{
    /// <summary>
    /// Host híbrido: usa el editor WebView2 cuando el runtime está disponible y
    /// logra inicializarse; en caso contrario recurre al editor nativo.
    /// Expone el mismo contrato (<see cref="IHtmlEditor"/>) para no tocar bindings.
    /// </summary>
    public partial class HybridHtmlEditor : System.Windows.Controls.UserControl, IHtmlEditor
    {
        private IHtmlEditor _active;
        private bool _initialized;
        private static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(15);
        // Airspace: WebView2 es una ventana real (HWND) y pinta por encima de todo
        // lo WPF cuando su zona se desplaza sobre otros controles (pestañas, botones).
        private System.Windows.Controls.ScrollViewer _outerScroll;

        public HybridHtmlEditor()
        {
            InitializeComponent();
            Loaded += HybridHtmlEditor_Loaded;
            Unloaded += HybridHtmlEditor_Unloaded;
        }

        public string Html
        {
            get => (string)GetValue(HtmlProperty);
            set => SetValue(HtmlProperty, value);
        }

        public static readonly DependencyProperty HtmlProperty =
            DependencyProperty.Register(nameof(Html), typeof(string), typeof(HybridHtmlEditor),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        private async void HybridHtmlEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            _initialized = true;
            HookOuterScroll();

            IHtmlEditor editor = null;
            string fallbackReason = "WebView2 no disponible en este equipo.";
            if (Services.WebView2Availability.IsAvailable)
            {
                var wv = new WebView2HtmlEditor();
                BindHtml(wv, WebView2HtmlEditor.HtmlProperty);
                // Mostrarlo ANTES de inicializar: WebView2 necesita estar en el árbol
                // visual para completar el arranque; crearlo en memoria y esperar el
                // init dejaba el arranque colgado (timeout).
                _active = wv;
                Host.Content = wv;
                try
                {
                    var init = wv.InitAsync();
                    bool finished = await Task.WhenAny(init, Task.Delay(InitTimeout)) == init;
                    bool ok = finished && await init;
                    if (ok)
                        editor = wv;
                    else
                    {
                        fallbackReason = !finished
                            ? "WebView2 no respondió a tiempo."
                            : (string.IsNullOrWhiteSpace(wv.InitError)
                                ? "WebView2 no se pudo iniciar."
                                : "WebView2 falló: " + wv.InitError);
                        System.Diagnostics.Debug.WriteLine(fallbackReason + " Usando editor nativo.");
                    }
                }
                catch (Exception ex)
                {
                    fallbackReason = "WebView2 falló: " + ex.Message;
                    System.Diagnostics.Debug.WriteLine(fallbackReason + ". Usando editor nativo.");
                }
            }

            if (editor == null)
            {
                var native = new NativeRichEditor();
                BindHtml(native, NativeRichEditor.HtmlProperty);
                _active = native;
                Host.Content = native;
                FallbackNotice.Text = "Modo compatibilidad: " + fallbackReason;
                FallbackNotice.ToolTip = $"Runtime WebView2: {Services.WebView2Availability.RuntimeVersion}\n{fallbackReason}";
                FallbackNotice.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Enlaza el Html del editor interno con el del host (two-way).</summary>
        private void BindHtml(System.Windows.Controls.UserControl inner, DependencyProperty htmlProperty)
            => inner.SetBinding(htmlProperty,
                new System.Windows.Data.Binding(nameof(Html)) { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });

        /// <summary>
        /// El HWND de WebView2 no lo recorta WPF: si su zona sale (aunque sea en parte)
        /// de la vista del ScrollViewer exterior, se oculta para que no pinte encima de
        /// pestañas y botones. Hidden conserva el espacio del layout; al volver a la
        /// vista reaparece solo.
        /// </summary>
        private void HookOuterScroll()
        {
            DependencyObject p = this;
            while ((p = System.Windows.Media.VisualTreeHelper.GetParent(p)) != null)
            {
                if (p is System.Windows.Controls.ScrollViewer sv)
                {
                    _outerScroll = sv;
                    sv.ScrollChanged += OuterScroll_Changed;
                    sv.SizeChanged += OuterScroll_Changed;
                    break;
                }
            }
            Dispatcher.BeginInvoke(new Action(UpdateWebVisibility),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void HybridHtmlEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_outerScroll != null)
            {
                _outerScroll.ScrollChanged -= OuterScroll_Changed;
                _outerScroll.SizeChanged -= OuterScroll_Changed;
                _outerScroll = null;
            }
        }

        private void OuterScroll_Changed(object sender, RoutedEventArgs e) => UpdateWebVisibility();

        // Último desplazamiento interno espejado (para no spamear al puente JS).
        private double _lastMirror = -1;

        /// <summary>
        /// Recorte por banda visible (airspace): el HWND de WebView2 no lo recorta WPF,
        /// así que cuando su zona sale parcialmente de la vista se encoge el control a
        /// la banda visible y se espeja el desplazamiento en el scroll interno de la
        /// página. Sin banda visible se oculta (Hidden conserva el layout).
        /// </summary>
        private void UpdateWebVisibility()
        {
            if (_outerScroll == null) return;
            var web = FindDescendantWebView2(this);
            if (web == null) return; // respaldo nativo: nada que recortar
            try
            {
                // La zona estable es el borde contenedor (el Web puede estar encogido
                // de un evento anterior y su ActualHeight mentiría).
                var area = System.Windows.Media.VisualTreeHelper.GetParent(web) as FrameworkElement;
                if (area == null) return;
                var rect = area.TransformToAncestor(_outerScroll)
                    .TransformBounds(new Rect(0, 0, area.ActualWidth, area.ActualHeight));
                var viewport = new Rect(0, 0, _outerScroll.ViewportWidth, _outerScroll.ViewportHeight);

                double topCut = System.Math.Max(0, -rect.Top);
                double botCut = System.Math.Max(0, rect.Bottom - viewport.Height);
                double visH = rect.Height - topCut - botCut;

                if (visH <= 8)
                {
                    if (web.Visibility != Visibility.Hidden) web.Visibility = Visibility.Hidden;
                    return;
                }

                if (web.Visibility != Visibility.Visible) web.Visibility = Visibility.Visible;

                if (topCut <= 1 && botCut <= 1)
                {
                    // Completo: banda completa; el scroll interno se deja como está.
                    web.Margin = new Thickness(0);
                    if (!double.IsNaN(web.Height)) web.Height = double.NaN;
                    _lastMirror = -1;
                    return;
                }

                web.Margin = new Thickness(0, topCut, 0, 0);
                web.Height = visH;
                // Espejar solo el recorte superior: el inferior no desplaza contenido.
                if (topCut > 1 && System.Math.Abs(topCut - _lastMirror) > 2)
                {
                    _lastMirror = topCut;
                    try { web.CoreWebView2?.ExecuteScriptAsync($"window.scrollTo(0,{(int)topCut})"); }
                    catch { /* espejo best-effort */ }
                }
            }
            catch (InvalidOperationException) { /* aún sin conectar al árbol */ }
        }

        private static Microsoft.Web.WebView2.Wpf.WebView2 FindDescendantWebView2(DependencyObject parent)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is Microsoft.Web.WebView2.Wpf.WebView2 wv) return wv;
                var found = FindDescendantWebView2(child);
                if (found != null) return found;
            }
            return null;
        }

        /// <inheritdoc/>
        public Task FlushAsync()
            => _active != null ? _active.FlushAsync() : Task.CompletedTask;

        /// <inheritdoc/>
        public Task InsertTextAtCaretAsync(string text)
            => _active != null ? _active.InsertTextAtCaretAsync(text) : Task.CompletedTask;
    }
}
