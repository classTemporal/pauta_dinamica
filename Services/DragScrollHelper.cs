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
        private const int TickMs = 25;           // cadencia del auto-scroll
        private const int WmMouseWheel = 0x020A;

        private FrameworkElement? _resolvedFor;
        private ScrollViewer? _target;
        private System.Windows.Point _cursor;                   // última posición del cursor relativa a _target
        private DispatcherTimer? _timer;

        // --- Hook global de la rueda (solo mientras hay un arrastre activo) ---
        private static DragScrollHelper? s_active;
        private static IntPtr s_hook;
        private static LowLevelMouseProc? s_hookProc; // referencia viva para evitar el GC del delegate

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Actualiza la posición del cursor y (re)inicia el auto-scroll y el hook de
        /// la rueda. Debe llamarse en DragEnter/DragOver con el elemento de la lista.
        /// </summary>
        public void Update(System.Windows.DragEventArgs e, FrameworkElement dropTarget)
        {
            if (dropTarget == null) return;

            if (!ReferenceEquals(_resolvedFor, dropTarget))
            {
                _resolvedFor = dropTarget;
                _target = ResolveScrollViewer(dropTarget);
            }

            var sv = _target;
            if (sv == null) return;

            _cursor = e.GetPosition(sv);

            if (_timer == null)
            {
                _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(TickMs), DispatcherPriority.Input, OnTick, sv.Dispatcher);
                _timer.Start();
            }
            else if (!_timer.IsEnabled)
            {
                _timer.Start();
            }

            EnsureHook();
        }

        /// <summary>
        /// Detiene el auto-scroll y desinstala el hook de la rueda. Idempotente.
        /// </summary>
        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }

            ReleaseHook();

            _target = null;
            _resolvedFor = null;
        }

        // ---------------- Auto-scroll ----------------

        private void OnTick(object? sender, EventArgs e)
        {
            var sv = _target;
            if (sv == null || sv.Dispatcher.HasShutdownStarted)
            {
                Stop();
                return;
            }

            if (!sv.IsVisible) return;
            double height = sv.ActualHeight;
            if (height <= 0) return;

            // Sin contenido desplazable no hay nada que hacer
            if (sv.ExtentHeight <= sv.ViewportHeight + 0.5) return;

            double y = _cursor.Y;
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

            // Aceleración: 1..4 pasos por tick según la proximidad al borde
            int steps = 1 + (int)(intensity * 3.0);
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
                if (nCode >= 0 && (int)wParam == WmMouseWheel && s_active != null)
                {
                    var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    int delta = (short)(info.mouseData >> 16);
                    if (delta != 0 && s_active.HandleWheel(info.pt, delta))
                    {
                        // Consumido: evita doble scroll si WPF también lo procesara
                        return (IntPtr)1;
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

            sv.Dispatcher.BeginInvoke(new Action(() =>
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
    }
}
