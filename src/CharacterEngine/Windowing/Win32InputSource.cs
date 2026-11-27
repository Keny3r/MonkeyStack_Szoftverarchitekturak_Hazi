namespace CharacterEngine.Windowing;

/// <summary>
/// Az <see cref="INativeInputSource"/> Win32 megvalósítása (Wrapper Facade): a nyers <see cref="NativeMethods"/> hívásokat
/// típusos, hibakezelt metódusok mögé csomagolja. Állapota nincs, ezért bármelyik szálról hívható (a fizika a léptető
/// szálán kérdezi le az egeret).
/// </summary>
public sealed class Win32InputSource : INativeInputSource
{
    public bool TryGetCursorPosition(out int x, out int y)
    {
        if (NativeMethods.GetCursorPos(out var point))
        {
            x = point.X;
            y = point.Y;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    public bool IsLeftButtonDown() => IsKeyDown(NativeMethods.VK_LBUTTON);

    public bool IsRightButtonDown() => IsKeyDown(NativeMethods.VK_RBUTTON);

    public bool AreMouseButtonsSwapped() => NativeMethods.GetSystemMetrics(NativeMethods.SM_SWAPBUTTON) != 0;

    public DpiAwareness GetDpiAwareness()
    {
        try
        {
            return NativeMethods.GetAwarenessFromDpiAwarenessContext(NativeMethods.GetThreadDpiAwarenessContext()) switch
            {
                NativeMethods.DPI_AWARENESS_UNAWARE => DpiAwareness.Unaware,
                NativeMethods.DPI_AWARENESS_PER_MONITOR_AWARE => DpiAwareness.PerMonitorAware,
                _ => DpiAwareness.SystemAware,
            };
        }
        catch (EntryPointNotFoundException)
        {
            // Windows 10 1607 előtt: rendszerszintű tudatosság (a WPF alapértelmezése), mint a platform-szolgáltatóban.
            return DpiAwareness.SystemAware;
        }
    }

    public uint GetSystemDpi() => NativeMethods.GetDpiForSystem();

    public uint? GetMonitorDpi(int x, int y)
    {
        var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = x, Y = y }, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return null;
        }

        return NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0
            ? dpiX
            : null;
    }

    private static bool IsKeyDown(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
}
