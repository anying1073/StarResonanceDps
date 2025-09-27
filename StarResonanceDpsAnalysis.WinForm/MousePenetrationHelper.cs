using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StarResonanceDpsAnalysis.WinForm
{
    public static class MousePenetrationHelper
    {
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_THICKFRAME = 0x00040000; // WS_SIZEBOX
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private const int LWA_ALPHA = 0x2;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
            => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
            => IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

        private static int? _backupStyle;
        private static int? _backupExStyle;

        // 方案 O：p 为“不透明度百分比”，0=全透明，100=完全不透明
        private static byte AlphaFromOpacityPercent(double p)
        {
            if (p < 0) p = 0;
            if (p > 100) p = 100;
            return (byte)Math.Round(p * 2.55, MidpointRounding.AwayFromZero); // 50% -> 128
        }

        private static void EnsureLayered(IntPtr hWnd)
        {
            var ex = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt32();
            if ((ex & WS_EX_LAYERED) == 0)
            {
                ex |= WS_EX_LAYERED;
                SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(ex));
            }
        }

        /// <summary>
        /// 仅更新不透明度（0~100，越大越不透明）。不改变穿透开关。
        /// </summary>
        public static void UpdateOpacityPercent(IntPtr hWnd, double opacityPercent)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return;
            EnsureLayered(hWnd);

            byte alpha = AlphaFromOpacityPercent(opacityPercent);
            SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA);
        }

        /// <summary>
        /// 开/关鼠标穿透；并按“不透明度百分比”设置不透明度。
        /// </summary>
        public static void SetMousePenetrate(IntPtr hWnd, bool enable, double opacityPercent = 100)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return;

            var style = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt32();
            var exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt32();

            if (enable)
            {
                if (_backupStyle == null) _backupStyle = style;
                if (_backupExStyle == null) _backupExStyle = exStyle;

                exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
                style &= ~WS_THICKFRAME;
                style &= ~WS_MAXIMIZEBOX;

                SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(exStyle));
                SetWindowLongPtr(hWnd, GWL_STYLE, new IntPtr(style));

                UpdateOpacityPercent(hWnd, opacityPercent); // 方案 O 映射
            }
            else
            {
                if (_backupExStyle.HasValue) { exStyle = _backupExStyle.Value; _backupExStyle = null; }
                exStyle &= ~WS_EX_TRANSPARENT;
                exStyle |= WS_EX_LAYERED; // 保留分层，方便继续调不透明度
                SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(exStyle));

                if (_backupStyle.HasValue) { style = _backupStyle.Value; _backupStyle = null; }
                SetWindowLongPtr(hWnd, GWL_STYLE, new IntPtr(style));

                // 恢复为完全不透明
                SetLayeredWindowAttributes(hWnd, 0, 255, LWA_ALPHA);
            }
        }

        public static bool IsPenetrating(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;
            var ex = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt32();
            return (ex & WS_EX_TRANSPARENT) == WS_EX_TRANSPARENT;
        }

        public static void SetMousePenetrate(Form form, bool enable, double opacityPercent = 100)
        {
            if (form == null || form.IsDisposed) return;
            SetMousePenetrate(form.Handle, enable, opacityPercent);

            if (enable)
            {
                form.MaximumSize = form.Size;
                form.MinimumSize = form.Size;
            }
            else
            {
                form.MinimumSize = System.Drawing.Size.Empty;
                form.MaximumSize = System.Drawing.Size.Empty;
            }
        }
    }
}
