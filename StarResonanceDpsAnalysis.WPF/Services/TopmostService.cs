using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Default WPF implementation of <see cref="ITopmostService"/>.
/// </summary>
/// <remarks>
/// Implementation details:
/// - Uses <see cref="Window.Topmost"/> for immediate WPF behavior.
/// - Applies native Z-order using SetWindowPos with HWND_TOPMOST/HWND_NOTOPMOST to ensure reliability.
/// - If the HWND is not created yet, defers native call until <see cref="Window.SourceInitialized"/>.
/// </remarks>
public sealed class TopmostService : ITopmostService
{
    private static readonly nint HWND_TOPMOST = new( -1 );
    private static readonly nint HWND_NOTOPMOST = new( -2 );

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    public void SetTopmost(Window window, bool enable)
    {
        // immediate WPF state
        if (window.IsVisible) window.SetCurrentValue(Window.TopmostProperty, enable);

        void ApplyNative()
        {
            var hWnd = new WindowInteropHelper(window).Handle;
            if (hWnd == nint.Zero) return;
            _ = SetWindowPos(hWnd,
                enable ? HWND_TOPMOST : HWND_NOTOPMOST,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        if (new WindowInteropHelper(window).Handle == nint.Zero)
        {
            void Handler(object? s, EventArgs e)
            {
                window.SourceInitialized -= Handler;
                ApplyNative();
            }
            window.SourceInitialized += Handler;
        }
        else
        {
            ApplyNative();
        }
    }

    public bool ToggleTopmost(Window window)
    {
        var newState = !window.Topmost;
        SetTopmost(window, newState);
        return newState;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
}
