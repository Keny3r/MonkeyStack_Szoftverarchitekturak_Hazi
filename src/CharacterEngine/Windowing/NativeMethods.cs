namespace CharacterEngine.Windowing;

using System.Runtime.InteropServices;

/// <summary>
/// A Karakter motor nyers Win32 deklarációi (egérbemenet, DPI, ablakstílusok). Csak a <c>Windowing</c> mappa típusos
/// burkolói (Wrapper Facade: <see cref="Win32InputSource"/>, később az overlay ablak kezelője) használják, a motor többi
/// része sosem hív közvetlenül natív kódot.
/// </summary>
/// <remarks>
/// Az OS esemény értesítő motornak saját, belső (<c>internal</c>) deklarációi vannak; azokat szándékosan nem használjuk,
/// mert egy másik komponens belső részletei. A DPI-kezelés viszont ugyanazt a szabályt követi, mint a platform-szolgáltató
/// (<c>OsEventEngine.Native.Win32DesktopScanner</c>), hogy a kurzor és a platformok ugyanabban a koordinátarendszerben legyenek.
/// </remarks>
internal static class NativeMethods
{
    internal const int VK_LBUTTON = 0x01;
    internal const int VK_RBUTTON = 0x02;
    internal const int SM_SWAPBUTTON = 23;

    internal const int DPI_AWARENESS_UNAWARE = 0;
    internal const int DPI_AWARENESS_SYSTEM_AWARE = 1;
    internal const int DPI_AWARENESS_PER_MONITOR_AWARE = 2;
    internal const int MDT_EFFECTIVE_DPI = 0;
    internal const uint MONITOR_DEFAULTTONEAREST = 2;

    internal const int GWL_EXSTYLE = -20;
    internal const long WS_EX_TRANSPARENT = 0x00000020;
    internal const long WS_EX_TOOLWINDOW = 0x00000080;
    internal const long WS_EX_LAYERED = 0x00080000;
    internal const long WS_EX_NOACTIVATE = 0x08000000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT point);

    /// <summary>A visszatérési érték legfelső bitje (0x8000) jelzi, hogy a gomb éppen le van-e nyomva.</summary>
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    internal static extern IntPtr GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    internal static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);

    /// <summary>Egy ablak bővített stílusának lekérdezése, 32 és 64 bites folyamatban is.</summary>
    internal static long GetWindowExStyle(IntPtr hWnd) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, GWL_EXSTYLE).ToInt64() : GetWindowLong32(hWnd, GWL_EXSTYLE);

    /// <summary>Egy ablak bővített stílusának beállítása, 32 és 64 bites folyamatban is.</summary>
    internal static void SetWindowExStyle(IntPtr hWnd, long style)
    {
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(hWnd, GWL_EXSTYLE, new IntPtr(style));
        }
        else
        {
            SetWindowLong32(hWnd, GWL_EXSTYLE, unchecked((int)style));
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int index, int value);
}
