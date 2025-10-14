using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StarResonanceDpsAnalysis.WPF.Services;

public interface IMousePenetrationService
{
    void SetMousePenetrate(Window window, bool enable);
}

public sealed class MousePenetrationService : IMousePenetrationService
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    private static nint GetWindowLongPtr(nint hWnd, int nIndex)
        => nint.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new nint(GetWindowLong32(hWnd, nIndex));

    private static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong)
        => nint.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new nint(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

    private static nint GetHandle(Window window) => new WindowInteropHelper(window).Handle;

    public void SetMousePenetrate(Window window, bool enable)
    {
        // Apply immediate WPF-level behavior regardless of native handle state
        window.IsHitTestVisible = !enable;

        void ApplyNative()
        {
            var hWnd = GetHandle(window);
            if (hWnd == nint.Zero) return;
            var exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt32();
            if (enable)
                exStyle |= WS_EX_TRANSPARENT;
            else
                exStyle &= ~WS_EX_TRANSPARENT;
            SetWindowLongPtr(hWnd, GWL_EXSTYLE, new nint(exStyle));
        }

        // If handle not ready yet, delay until SourceInitialized
        if (GetHandle(window) == nint.Zero)
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
}
