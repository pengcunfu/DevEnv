using System;
using System.Runtime.InteropServices;

namespace ProgramBox.Utils
{
    /// <summary>
    /// 为无边框窗口提供原生边缘缩放。
    /// 通过拦截 WM_NCHITTEST 返回边缘命中区域，由系统接管缩放与光标。
    /// </summary>
    public static class WindowResizeHelper
    {
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_STYLECHANGED = 0x007D;

        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private const int GWLP_WNDPROC = -4;
        private const int GWL_STYLE = -16;
        private const int WS_THICKFRAME = 0x00040000;

        /// <summary>
        /// 缩放热区厚度（逻辑像素）
        /// </summary>
        private const double ResizeBorderThickness = 6;

        private static WndProcDelegate? _procDelegate;
        private static IntPtr _originalProc;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// 挂接窗口过程，启用边缘缩放。重复调用不会重复挂接。
        /// </summary>
        public static void Attach(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || _procDelegate != null)
                return;

            _procDelegate = WndProc;
            var procPtr = Marshal.GetFunctionPointerForDelegate(_procDelegate);

            // 已挂接过则直接返回
            if (GetWindowLongPtr(hwnd, GWLP_WNDPROC) == procPtr)
                return;

            _originalProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, procPtr);
            EnsureThickFrame(hwnd);
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_NCHITTEST)
            {
                var hit = HitTest(hWnd, lParam);
                if (hit != HTCLIENT)
                    return new IntPtr(hit);
            }

            var result = CallWindowProc(_originalProc, hWnd, msg, wParam, lParam);

            // Avalonia 在窗口状态变化时会重新应用窗口样式并移除 WS_THICKFRAME，
            // 这里在样式被修改后再次补上，保证边缘缩放一直可用。
            if (msg == WM_STYLECHANGED)
            {
                EnsureThickFrame(hWnd);
            }

            return result;
        }

        private static void EnsureThickFrame(IntPtr hWnd)
        {
            var style = GetWindowLong32(hWnd, GWL_STYLE);
            if ((style & WS_THICKFRAME) == 0)
            {
                SetWindowLong32(hWnd, GWL_STYLE, style | WS_THICKFRAME);
            }
        }

        private static int HitTest(IntPtr hWnd, IntPtr lParam)
        {
            // 最大化时不允许边缘缩放
            if (IsZoomed(hWnd))
                return HTCLIENT;

            if (!GetWindowRect(hWnd, out var rect))
                return HTCLIENT;

            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

            var scale = Math.Max(1.0, GetDpiForWindow(hWnd) / 96.0);
            int thickness = Math.Max(4, (int)(ResizeBorderThickness * scale));

            bool left = x >= rect.Left && x <= rect.Left + thickness;
            bool right = x >= rect.Right - thickness && x <= rect.Right;
            bool top = y >= rect.Top && y <= rect.Top + thickness;
            bool bottom = y >= rect.Bottom - thickness && y <= rect.Bottom;

            if (left && top) return HTTOPLEFT;
            if (right && top) return HTTOPRIGHT;
            if (left && bottom) return HTBOTTOMLEFT;
            if (right && bottom) return HTBOTTOMRIGHT;
            if (left) return HTLEFT;
            if (right) return HTRIGHT;
            if (top) return HTTOP;
            if (bottom) return HTBOTTOM;

            return HTCLIENT;
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
    }
}
