using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using PautaDinamicaApp.ViewModels;

namespace PautaDinamicaApp.Views.HtmlEditor
{
    public partial class RichHtmlEditor : System.Windows.Controls.UserControl
    {
        private const string BridgeName = "hostBridge";
        private readonly HtmlBridge _bridge = new();
        private bool _isReady;
        private bool _suppressChange;
        private string _pendingHtml = "";
        private string _lastKnownHtml = "";

        public RichHtmlEditor()
        {
            InitializeComponent();

            // NO se asigna DataContext = this a propósito: la ventana enlaza Html a
            // SelectedPauta.EmailBodyHtmlTemplate, y ese binding se resuelve contra el
            // DataContext heredado. La barra de herramientas usa RelativeSource AncestorType,
            // así que no necesita que el DataContext sea el propio control.

            ExecCommand = new RelayCommand(p => _ = ExecAsync(p as string ?? ""));
            InsertTableCommand = new RelayCommand(p => _ = InsertTableAsync(p as string ?? ""));
            PickImageCommand = new RelayCommand(_ => _ = PickImageAsync());
            InsertLinkCommand = new RelayCommand(_ => _ = InsertLinkAsync());
            InsertFieldRequested = new RelayCommand(_ => RaiseInsertFieldRequested());
            InsertDateRequested = new RelayCommand(_ => RaiseInsertDateRequested());

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public string Html
        {
            get => (string)GetValue(HtmlProperty);
            set => SetValue(HtmlProperty, value);
        }

        /// <summary>
        /// Cuerpo HTML del correo. Debe ser una <see cref="DependencyProperty"/> porque el XAML
        /// de <c>SettingsWindow</c> la enlaza a <c>SelectedPauta.EmailBodyHtmlTemplate</c>: WPF
        /// solo admite <c>Binding</c> sobre propiedades de dependencia.
        /// </summary>
        public static readonly DependencyProperty HtmlProperty =
            DependencyProperty.Register(nameof(Html), typeof(string), typeof(RichHtmlEditor),
                new PropertyMetadata("", OnHtmlChanged));

        private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (RichHtmlEditor)d;
            editor._lastKnownHtml = e.NewValue as string ?? "";

            // Si el WebView2 aún no está listo, LoadHtmlAsync deja el HTML en _pendingHtml
            // y se aplica cuando llega el mensaje "Ready" del script.
            _ = editor.LoadHtmlAsync(editor._lastKnownHtml);
        }

        public string StatusText
        {
            get => (string)GetValue(StatusTextProperty);
            set => SetValue(StatusTextProperty, value);
        }

        public static readonly DependencyProperty StatusTextProperty =
            DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(RichHtmlEditor),
                new PropertyMetadata("Listo"));

        public event EventHandler? HtmlChanged;
        public event EventHandler? FieldInsertRequested;
        public event EventHandler? DateInsertRequested;

        public ICommand InsertFieldRequested { get; }
        public ICommand InsertDateRequested { get; }

        public ICommand ExecCommand { get; }
        public ICommand InsertTableCommand { get; }
        public ICommand PickImageCommand { get; }
        public ICommand InsertLinkCommand { get; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _bridge.MessageReceived += OnBridgeMessage;
            _ = InitializeAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _bridge.MessageReceived -= OnBridgeMessage;
            try { Browser?.Dispose(); } catch { }
        }

        private async Task InitializeAsync()
        {
            try
            {
                // FIX (E_ACCESSDENIED / 0x80070005): EnsureCoreWebView2Async sin argumentos hace
                // que WebView2 cree su carpeta de datos en una ubicación derivada del ejecutable.
                // Si esa ruta no es escribible (instalación en red, carpeta restringida, OneDrive,
                // etc.) falla con "Acceso denegado" y el editor queda inutilizable aunque el
                // runtime de Edge SÍ esté instalado. Se fuerza una carpeta garantizadamente
                // escribible en %LOCALAPPDATA% para que funcione en cualquier equipo.
                var env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: GetUserDataFolder());

                await Browser.EnsureCoreWebView2Async(env);
                Browser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                Browser.CoreWebView2.AddHostObjectToScript(BridgeName, _bridge);
                Browser.NavigateToString(BuildPage(Html));

                _isReady = true;
                RuntimeWarning.Visibility = Visibility.Collapsed;
                StatusText = "Listo";
            }
            catch (Exception ex)
            {
                _isReady = false;
                Browser.Visibility = Visibility.Collapsed;
                RuntimeWarning.Visibility = Visibility.Visible;
                StatusText = "Editor no disponible: " + ex.Message;
            }
        }

        /// <summary>
        /// Carpeta de datos de WebView2 dentro de %LOCALAPPDATA%, que siempre es escribible por
        /// el usuario actual. Evita el error de acceso denegado cuando la app no puede crear
        /// archivos junto al ejecutable.
        /// </summary>
        private static string GetUserDataFolder()
        {
            string folder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PautaDinamica",
                "WebView2");

            try { System.IO.Directory.CreateDirectory(folder); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("no se pudo crear la carpeta de WebView2: " + ex.Message);
            }

            return folder;
        }


        private static string BuildPage(string initialHtml)
        {
            string template = ReadEditorTemplate();
            return template.Replace(
                "$INITIAL_HTML$",
                (initialHtml ?? "").Replace("\\", "\\\\").Replace("'", "\\'"));
        }

        private static string ReadEditorTemplate()
        {
            var asm = Assembly.GetExecutingAssembly();
            const string suffix = "Views.HtmlEditor.editor.html";

            foreach (string name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    using Stream? s = asm.GetManifestResourceStream(name);
                    if (s == null) continue;
                    using var reader = new StreamReader(s);
                    return reader.ReadToEnd();
                }
            }

            return "<!DOCTYPE html><html><head><meta charset='utf-8'></head><body>" +
                   "<div id='editor' contenteditable='true'></div></body></html>";
        }

        #region Comunicación con el script

        private void OnBridgeMessage(EditorMessage message)
        {
            switch (message.Kind)
            {
                case EditorMessageKind.Ready:
                    _isReady = true;
                    if (!string.IsNullOrEmpty(_pendingHtml))
                    {
                        _ = RunScriptAsync("window.editorApi.setHtml(" + EscapeForScriptStr(_pendingHtml) + ")");
                        _pendingHtml = "";
                    }
                    break;

                case EditorMessageKind.Changed:
                    if (_suppressChange) return;
                    _lastKnownHtml = message.Html ?? "";
                    HtmlChanged?.Invoke(this, EventArgs.Empty);
                    break;

                case EditorMessageKind.InsertField:
                    RaiseInsertFieldRequested();
                    break;

                case EditorMessageKind.InsertDate:
                    RaiseInsertDateRequested();
                    break;
            }
        }

        private static string EscapeForScriptStr(string value)
        {
            return (value ?? "")
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private async Task RunScriptAsync(string script)
        {
            if (!_isReady || Browser.CoreWebView2 == null) return;
            try { await Browser.ExecuteScriptAsync(script); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("script falló: " + ex.Message); }
        }

        private async Task<string> RunScriptForResultAsync(string script)
        {
            if (!_isReady || Browser.CoreWebView2 == null) return "";
            try { return await Browser.ExecuteScriptAsync(script) ?? ""; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("script falló: " + ex.Message); return ""; }
        }

        #endregion

        #region Operaciones

        public async Task LoadHtmlAsync(string html)
        {
            html ??= "";
            _lastKnownHtml = html;
            if (!_isReady)
            {
                _pendingHtml = html;
                return;
            }

            _suppressChange = true;
            try { await RunScriptAsync("window.editorApi.setHtml(" + EscapeForScriptStr(html) + ")"); }
            finally { _suppressChange = false; }
        }

        public async Task<string> FlushAsync()
        {
            if (!_isReady) return _lastKnownHtml;
            string raw = await RunScriptForResultAsync("window.editorApi.getHtml()");
            string html = JsonSerializer.Deserialize<string>(raw) ?? raw;
            if (!string.IsNullOrEmpty(html)) _lastKnownHtml = html;
            return _lastKnownHtml;
        }

        public async Task InsertTextAtCaretAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!_isReady)
            {
                _lastKnownHtml += text;
                HtmlChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            await RunScriptAsync("window.editorApi.insertText(" + EscapeForScriptStr(text) + ")");
            await RunScriptAsync("window.editorApi.focus()");
        }

        private async Task ExecAsync(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            string name = command;
            string? argument = null;
            int colon = command.IndexOf(':');
            if (colon > 0) { name = command.Substring(0, colon); argument = command.Substring(colon + 1); }

            string script = argument == null
                ? "window.editorApi.exec('" + name + "')"
                : "window.editorApi.exec('" + name + "', " + EscapeForScriptStr(argument) + ")";

            await RunScriptAsync(script);
            await RunScriptAsync("window.editorApi.focus()");
        }

        private async Task InsertTableAsync(string dimension)
        {
            var parts = (dimension ?? "").Split(',');
            if (parts.Length != 2) return;
            if (!int.TryParse(parts[0], out int rows)) return;
            if (!int.TryParse(parts[1], out int cols)) return;
            await RunScriptAsync("window.editorApi.insertTable(" + rows + ", " + cols + ")");
            await RunScriptAsync("window.editorApi.focus()");
        }

        private async Task PickImageAsync()
        {
            await RunScriptAsync("window.editorApi.pickImage()");
        }

        private async Task InsertLinkAsync()
        {
            string? url = PromptForUrl();
            if (string.IsNullOrWhiteSpace(url)) return;
            await RunScriptAsync("window.editorApi.exec('createLink', " + EscapeForScriptStr(url) + ")");
            await RunScriptAsync("window.editorApi.focus()");
        }

        #endregion

        private string? PromptForUrl()
        {
            var win = new Window
            {
                Title = "Insertar enlace",
                Width = 420,
                Height = 175,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = TryFindResource("CardBackgroundBrush") as System.Windows.Media.Brush
                             ?? System.Windows.Media.Brushes.White,
                Owner = Window.GetWindow(this),
                WindowStyle = WindowStyle.ToolWindow
            };

            var panel = new StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new TextBlock
            {
                Text = "Dirección del enlace:", Margin = new Thickness(0, 0, 0, 8),
                Foreground = TryFindResource("TextBrush") as System.Windows.Media.Brush
            });

            var box = new System.Windows.Controls.TextBox { Height = 32, VerticalContentAlignment = VerticalAlignment.Center };
            panel.Children.Add(box);

            var buttons = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var ok = new System.Windows.Controls.Button
            {
                Content = "Aceptar", Width = 95, Height = 32, Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true, Style = TryFindResource("PrimaryButton") as System.Windows.Style
            };
            ok.Click += (s, e) => { win.DialogResult = true; };

            var cancel = new System.Windows.Controls.Button
            {
                Content = "Cancelar", Width = 95, Height = 32, IsCancel = true,
                Style = TryFindResource("GhostButton") as System.Windows.Style
            };

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            win.Content = panel;

            box.Focus();
            return win.ShowDialog() == true ? box.Text : null;
        }

        private void RaiseInsertFieldRequested()
            => FieldInsertRequested?.Invoke(this, EventArgs.Empty);

        private void RaiseInsertDateRequested()
            => DateInsertRequested?.Invoke(this, EventArgs.Empty);
    }
}
