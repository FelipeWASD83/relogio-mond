using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RelogioMond;

static class Nativo
{
    public const int GWL_EXSTYLE = -20;
    public const int GWLP_HWNDPARENT = -8;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WS_EX_LAYERED = 0x80000;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_APPWINDOW = 0x40000;

    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int MA_NOACTIVATE = 3;
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_DPICHANGED = 0x02E0;

    public static readonly IntPtr HWND_BOTTOM = new(1);
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd, hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? cls, string? nome);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string s);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int estado);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int valor, int tamanho);

    public static readonly IntPtr DPI_PER_MONITOR_V2 = new(-4);

    public static string Classe(IntPtr h)
    {
        var sb = new StringBuilder(128);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool EhAreaDeTrabalho(IntPtr h)
    {
        var c = Classe(h);
        return c is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    // Janela "escondida" pelo DWM (apps UWP suspensos, janelas de outra área de trabalho virtual).
    public static bool Encoberta(IntPtr h)
    {
        const int DWMWA_CLOAKED = 14;
        return DwmGetWindowAttribute(h, DWMWA_CLOAKED, out var v, 4) == 0 && v != 0;
    }

    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);

    // Jogo ou vídeo em tela cheia, apresentação: o relógio para de desenhar as barras. O aviso do Windows
    // vale para qualquer monitor (um vídeo em tela cheia no monitor em pé também o acende), então para os
    // estados 2 e 3 só conta se a janela em primeiro plano está no mesmo monitor do relógio.
    public static bool TelaCheiaOcupada(IntPtr relogio)
    {
        if (SHQueryUserNotificationState(out var e) != 0) return false;
        if (e == 4) return true;
        if (e != 2 && e != 3) return false;
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || relogio == IntPtr.Zero) return true;
        // clicar na área de trabalho deixa a Progman em primeiro plano e o Windows segue dizendo "ocupado":
        // isso não é app em tela cheia
        if (EhAreaDeTrabalho(fg)) return false;
        const uint MONITOR_DEFAULTTONEAREST = 2;
        var monitor = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
        if (monitor != MonitorFromWindow(relogio, MONITOR_DEFAULTTONEAREST)) return false;
        // e a janela precisa ocupar o monitor inteiro (com folga de 2 px)
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(fg, out var r)) return true;
        return r.Left <= info.rcMonitor.Left + 2 && r.Top <= info.rcMonitor.Top + 2 &&
               r.Right >= info.rcMonitor.Right - 2 && r.Bottom >= info.rcMonitor.Bottom - 2;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
}
