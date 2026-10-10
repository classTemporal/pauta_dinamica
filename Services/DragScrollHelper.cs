using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PautaDinamicaApp.Services
{
    /// <summary>
    /// Utilidad compartida por todas las ventanas con listas draggables
    /// (selector de plantillas, gestión de plantillas, opciones, configuración
    /// de pauta, reglas de correo y dashboard).
    ///
    /// Mientras dura un DragDrop.DoDragDrop ofrece:
    ///
    ///  1) Auto-scroll: al mantener el cursor cerca del borde superior/inferior del
    ///     área con scroll se desplaza la lista automáticamente, con aceleración según
    ///     la proximidad al borde. Usa la última posición reportada por DragOver porque
    ///     Mouse.GetPosition queda congelada mientras OLE procesa el arrastre
    ///     (por eso el auto-scroll anterior no funcionaba).
    ///
    ///  2) Rueda del ratón manual: WPF/OLE no entrega WM_MOUSEWHEEL a la ventana
    ///     durante DoDragDrop (bug conocido: dotnet/wpf#7694), por lo que se instala
    ///     un hook global de bajo nivel (WH_MOUSE_LL) solo mientras dura el arrastre
    ///     para capturar la rueda y desplazar el ScrollViewer bajo el cursor.
    ///
    /// Uso: llamar <see cref="Update"/> desde DragOver y <see cref="Stop"/> al
    /// terminar el arrastre (en el finally de DoDragDrop). Stop() es idempotente.
    /// </summary>
    public sealed class DragScrollHelper
    {
        private const double EdgeZone = 60.0;   // px desde el borde donde inicia el auto-scroll
        private const int TickMs = 60;           // cadencia del timer de respaldo
        private const int HookMinIntervalMs = 50; // intervalo mínimo entre scrolls del hook
        private const int MaxStepsPerTick = 2;   // tope de líneas por evento (antes 4)
        private const int WmMouseWheel = 0x020A;
        private const int WmMouseMove = 0x0200;
        private const int WmLButtonUp = 0x0202;

        // --- Estado del arrastre: el hook WH_MOUSE_LL es el motor. ---
        // Durante DoDragDrop el message pump de WPF/OLE no despacha el
        // DispatcherTimer (por eso el auto-scroll anterior jamás avanzaba),
        // pero los hooks de bajo nivel SÍ reciben eventos. Así que cada
        // movimiento de ratón (WM_MOUSEMOVE) que capture el hook actualiza la
        // posición y hace el auto-scroll de forma síncrona.

        private ScrollViewer? _target;
        private FrameworkElement? _dropTarget;
        private System.Threading.Timer? _timer;
        private bool _running;
        private long _lastHookScrollTick; // Environment.TickCount64 del último scroll por hook

        // --- Hook global de la rueda (solo mientras hay un arrastre activo) ---
        private static DragScrollHelper? s_active;
        private static IntPtr s_hook;
        private static LowLevelMouseProc? s_hookProc; // referencia viva para evitar el GC del delegate

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Instancia compartida por todas las ventanas (solo hay un arrastre activo a la vez).
        /// </summary>
        public static DragScrollHelper Current { get; } = new DragScrollHelper();

        /// <summary>
        /// Inicia el auto-scroll y el hook de la rueda para el arrastre que está por
        /// comenzar. Debe llamarse justo antes de DragDrop.DoDragDrop, pasando el
        /// elemento contenedor de la lista (ListBox/ItemsControl/ScrollViewer).
        /// </summary>
        public void BeginDrag(FrameworkElement dropTarget)
        {
            if (dropTarget == null) return;

            Stop(); // por si quedó un arrastre anterior sin cerrar

            _dropTarget = dropTarget;
            _target = ResolveScrollViewer(dropTarget);
            _running = true;

            // Timer de respaldo (hilo de pool): sigue latiendo durante
            // DoDragDrop aunque el Dispatcher esté bloqueado en el bucle OLE.
            _timer = new System.Threading.Timer(OnTimerTick, null, TickMs, TickMs);

            EnsureHook();
        }

        /// <summary>
        /// Detiene el auto-scroll y desinstala el hook de la rueda. Idempotente.
        /// </summary>
        public void Stop()
        {
            _running = false;

            try { _timer?.Dispose(); } catch { /* ya liberado */ }
            _timer = null;

            ReleaseHook();

            _target = null;
            _dropTarget = null;
        }

        /// <summary>
        /// Re-resuelve el ScrollViewer objetivo (las listas virtualizadas pueden
        /// recrear su ScrollViewer interno al hacer scroll) y reanuda el
        /// auto-scroll. Llamar desde DragOver.
        /// </summary>
        public void Update(System.Windows.DragEventArgs e, FrameworkElement? dropTarget = null)
        {
            if (!_running) return;
            FrameworkElement? anchor = dropTarget ?? _dropTarget;
            if (anchor == null) return;

            var resolved = ResolveScrollViewer(anchor);
            if (resolved != null) _target = resolved;
        }

        /// <summary>
        /// Mantiene el hook activo pero frena el auto-scroll mientras el cursor
        /// está fuera del área (DragLeave). Al re-entrar, Update() lo reanuda.
        /// Implementado como no-op de compatibilidad: el auto-scroll solo actúa
        /// cuando el cursor está dentro de la zona de borde del ScrollViewer.
        /// </summary>
        public void PauseAutoScroll()
        {
            // Intencionalmente vacío: el Tick ya verifica la posición del cursor
            // y no scrollea si está fuera del área. Se conserva por compatibilidad
            // con las ventanas que lo llaman desde DragLeave.
        }

        // ---------------- Auto-scroll ----------------

        /// <summary>
        /// Tick de respaldo (hilo de pool): usa la última posición conocida del
        /// cursor capturada por el hook y pide el scroll en el Dispatcher.
        /// </summary>
        private void OnTimerTick(object? state)
        {
            if (!_running) return;
            var sv = _target;
            if (sv == null) return;
            if (!GetCursorPos(out POINT pt)) return;

            try
            {
                sv.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_running) return;
                    AutoScrollAtScreenPoint(pt);
                }), DispatcherPriority.Input);
            }
            catch { /* dispatcher cerrándose */ }
        }

        /// <summary>
        /// Auto-scroll síncrono en el hilo del hook: funciona dentro del bucle
        /// modal de DoDragDrop porque el hilo del hook NO está bloqueado por OLE.
        /// Invoca el scroll por el Dispatcher de forma síncrona (Invoke, no
        /// BeginInvoke) para que el desplazamiento ocurra de inmediato.
        /// </summary>
        private void AutoScrollFromHook(POINT screenPt)
        {
            var sv = _target;
            if (sv == null || !_running) return;

            // Limitador: el hook recibe decenas de WM_MOUSEMOVE por segundo;
            // sin esto cada micro-movimiento scrollea y se vuelve incontrolable.
            long now = Environment.TickCount64;
            if (now - _lastHookScrollTick < HookMinIntervalMs) return;
            _lastHookScrollTick = now;

            try
            {
                if (sv.Dispatcher.HasShutdownStarted || sv.Dispatcher.HasShutdownFinished) return;
                sv.Dispatcher.Invoke(() =>
                {
                    if (!_running) return;
                    AutoScrollAtScreenPoint(screenPt);
                }, DispatcherPriority.Input);
            }
            catch { /* dispatcher ocupado/cerrándose: el timer lo reintentará */ }
        }

        /// <summary>
        /// Debe ejecutarse en el hilo de UI. Desplaza si el cursor está en la
        /// zona de borde del ScrollViewer objetivo.
        /// </summary>
        private void AutoScrollAtScreenPoint(POINT screenPt)
        {
            var sv = _target;
            if (sv == null || !sv.IsVisible) return;
            double height = sv.ActualHeight;
            if (height <= 0) return;

            // Sin contenido desplazable no hay nada que hacer (pero no fallar:
            // el contenido puede crecer al reordenar).
            if (sv.ExtentHeight <= sv.ViewportHeight + 0.5) return;

            // GetCursorPos devuelve píxeles de pantalla y PointFromScreen los
            // convierte a coordenadas del visual (ya compensa el DPI).
            // Funciona durante DoDragDrop, a diferencia de Mouse.GetPosition
            // que WPF congela en el bucle OLE.
            System.Windows.Point local;
            try
            {
                local = sv.PointFromScreen(new System.Windows.Point(screenPt.X, screenPt.Y));
            }
            catch
            {
                return; // el elemento no tiene ventana (o se está cerrando)
            }

            double y = local.Y;
            int direction;
            double intensity; // 0..1: qué tan profundo está el cursor dentro de la zona de borde

            if (y < EdgeZone)
            {
                direction = -1;
                intensity = 1.0 - Math.Max(y, 0) / EdgeZone;
            }
            else if (y > height - EdgeZone)
            {
                direction = 1;
                intensity = 1.0 - Math.Max(height - y, 0) / EdgeZone;
            }
            else
            {
                return; // el cursor está en la zona segura: no se scrollea
            }

            // Aceleración suave: 1..2 pasos por tick según la proximidad al borde.
            // Pegado al borde scrollea el doble de rápido que en la orilla de la zona.
            int steps = 1 + (int)(intensity * (MaxStepsPerTick - 1));
            for (int i = 0; i < steps; i++)
            {
                if (direction < 0) sv.LineUp();
                else sv.LineDown();
            }
        }

        /// <summary>
        /// Resuelve el ScrollViewer que debe desplazarse: primero el interno del propio
        /// elemento (plantilla del ListBox) si tiene desbordamiento; si no, uno ancestro
        /// (caso ItemsControl dentro de un ScrollViewer explícito, p.ej. el dashboard).
        /// </summary>
        private static ScrollViewer? ResolveScrollViewer(FrameworkElement element)
        {
            var inner = FindVisualChild<ScrollViewer>(element);
            if (CanScroll(inner)) return inner;

            var outer = FindVisualParent<ScrollViewer>(element);
            if (CanScroll(outer)) return outer;

            return inner ?? outer;
        }

        private static bool CanScroll(ScrollViewer? sv)
            => sv != null && sv.IsVisible && sv.ExtentHeight > sv.ViewportHeight + 0.5;

        // ---------------- Rueda del ratón durante el arrastre ----------------

        private void EnsureHook()
        {
            s_active = this;
            if (s_hook != IntPtr.Zero) return;

            try
            {
                s_hookProc = HookCallback;
                s_hook = SetWindowsHookEx(WH_MOUSE_LL, s_hookProc, GetModuleHandle(null), 0);
                if (s_hook == IntPtr.Zero)
                {
                    s_hookProc = null;
                    System.Diagnostics.Debug.WriteLine($"DragScrollHelper: no se pudo instalar el hook de la rueda (error {Marshal.GetLastWin32Error()}).");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DragScrollHelper: excepción instalando hook de rueda: " + ex.Message);
            }
        }

        private static void ReleaseHook()
        {
            s_active = null;
            if (s_hook == IntPtr.Zero) return;

            try { UnhookWindowsHookEx(s_hook); }
            catch { /* ya liberado */ }
            s_hook = IntPtr.Zero;
            s_hookProc = null;
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                var active = s_active;
                if (nCode >= 0 && active != null)
                {
                    int msg = (int)wParam;
                    if (msg == WmMouseWheel)
                    {
                        var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                        int delta = (short)(info.mouseData >> 16);
                        if (delta != 0 && active.HandleWheel(info.pt, delta))
                        {
                            // Consumido: evita doble scroll si WPF también lo procesara
                            return (IntPtr)1;
                        }
                    }
                    else if (msg == WmMouseMove)
                    {
                        // Motor del auto-scroll: cada movimiento del ratón hace
                        // scroll síncrono si el cursor está en la zona de borde.
                        // El hook se ejecuta fuera del bucle modal de OLE, así que
                        // esto SÍ avanza durante DoDragDrop.
                        var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                        active.AutoScrollFromHook(info.pt);
                    }
                    else if (msg == WmLButtonUp)
                    {
                        // Red de seguridad: si el DoDragDrop terminó sin pasar por
                        // el finally (p.ej. drop en otra app), soltar el hook para
                        // no dejarlo instalado para siempre.
                        var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                        active.AutoScrollFromHook(info.pt);
                    }
                }
            }
            catch
            {
                // Nunca dejar excepciones escapar del hook: bloquearía la entrada del sistema
            }

            return CallNextHookEx(s_hook, nCode, wParam, lParam);
        }

        /// <summary>
        /// Procesa un evento de rueda capturado por el hook.
        /// Devuelve true si la rueda fue consumida (el cursor está sobre el área con scroll).
        /// </summary>
        private bool HandleWheel(POINT screenPoint, int delta)
        {
            var sv = _target;
            if (sv == null || !sv.IsVisible) return false;
            if (sv.ExtentHeight <= sv.ViewportHeight + 0.5) return false;

            System.Windows.Point local;
            try
            {
                // PointFromScreen y el pt del hook trabajan en píxeles de dispositivo: coinciden
                local = sv.PointFromScreen(new System.Windows.Point(screenPoint.X, screenPoint.Y));
            }
            catch
            {
                return false; // el elemento no tiene ventana (o se está cerrando)
            }

            if (local.X < 0 || local.Y < 0 || local.X > sv.ActualWidth || local.Y > sv.ActualHeight)
                return false;

            bool up = delta > 0;
            int notches = Math.Max(1, Math.Abs(delta) / 120);
            int linesPerNotch = (int)SystemParameters.WheelScrollLines; // -1 = desplazar una página

            // Síncrono (Invoke): el Dispatcher está dentro del bucle modal de
            // OLE durante DoDragDrop; BeginInvoke se encolaría y el scroll
            // manual jamás se vería hasta soltar el botón.
            try
            {
                sv.Dispatcher.Invoke(new Action(() =>
                {
                    if (!sv.IsVisible) return;

                    if (linesPerNotch < 0)
                    {
                        for (int n = 0; n < notches; n++)
                        {
                            if (up) sv.PageUp(); else sv.PageDown();
                        }
                    }
                    else
                    {
                        int steps = notches * Math.Max(1, linesPerNotch);
                        for (int i = 0; i < steps; i++)
                        {
                            if (up) sv.LineUp(); else sv.LineDown();
                        }
                    }
                }), DispatcherPriority.Input);
            }
            catch { /* dispatcher ocupado/cerrándose */ }

            return true;
        }

        // ---------------- Búsqueda en el árbol visual ----------------

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var nested = FindVisualChild<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            var current = child == null ? null : VisualTreeHelper.GetParent(child);
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // ---------------- Win32 ----------------

        private const int WH_MOUSE_LL = 14;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);
    }
}
