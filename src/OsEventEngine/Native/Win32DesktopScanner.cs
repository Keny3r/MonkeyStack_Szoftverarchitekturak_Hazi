namespace OsEventEngine.Native;

using MonkeyStack.Core.Character;

/// <summary>
/// Az <see cref="IDesktopScanner"/> Win32 megvalósítása (Wrapper Facade az <c>EnumWindows</c>, a DWM,
/// az <c>EnumDisplayMonitors</c> és a DPI API-k fölött). A szűrés megegyezik az ablakszámlálóéval
/// (<see cref="WindowEnumerator.IsUserWindow"/>), kiegészítve a minimalizált és a shell-ablakok kizárásával.
/// </summary>
internal sealed class Win32DesktopScanner : IDesktopScanner
{
    private const double BaseDpi = 96.0;

    // A tálca és az asztal: ezek nem platformok, a talajt a monitor munkaterülete adja.
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Progman",
        "WorkerW",
    };

    public DesktopScene Scan()
    {
        int awareness = GetDpiAwareness();
        return new DesktopScene(GetWindows(awareness), GetMonitors(awareness));
    }

    private static List<SceneWindow> GetWindows(int awareness)
    {
        var result = new List<SceneWindow>();
        var className = new char[64];
        NativeMethods.EnumWindowsProc callback = (hWnd, _) =>
        {
            if (!WindowEnumerator.IsUserWindow(hWnd) || NativeMethods.IsIconic(hWnd))
            {
                return true;
            }

            int length = NativeMethods.GetClassName(hWnd, className, className.Length);
            if (length > 0 && ShellClasses.Contains(new string(className, 0, length)))
            {
                return true;
            }

            if (TryGetBounds(hWnd, awareness, out var bounds) && bounds.Width > 0 && bounds.Height > 0)
            {
                result.Add(new SceneWindow(bounds, NativeMethods.IsZoomed(hWnd)));
            }

            return true;
        };

        NativeMethods.EnumWindows(callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return result;
    }

    private static bool TryGetBounds(IntPtr hWnd, int awareness, out ScreenRect bounds)
    {
        // Windows 10+ alatt a GetWindowRect a láthatatlan (~7 px-es) átméretező keretet is tartalmazza,
        // ezért a DWM kiterjesztett keretét használjuk. A DWM mindig fizikai pixelt ad, ezért DPI-tudatlan
        // folyamatnál (virtualizált koordináták) a GetWindowRect marad, a keret pontatlanságával együtt.
        int size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>();
        bool useDwm = awareness != NativeMethods.DPI_AWARENESS_UNAWARE;
        NativeMethods.RECT rect = default;
        bool ok = (useDwm && NativeMethods.DwmGetWindowRect(hWnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out rect, size) == 0)
            || NativeMethods.GetWindowRect(hWnd, out rect);
        if (!ok)
        {
            bounds = default;
            return false;
        }

        bounds = ToRect(rect);
        return true;
    }

    private static List<SceneMonitor> GetMonitors(int awareness)
    {
        var result = new List<SceneMonitor>();
        double systemScale = awareness == NativeMethods.DPI_AWARENESS_SYSTEM_AWARE ? NativeMethods.GetDpiForSystem() / BaseDpi : 1.0;

        NativeMethods.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr _, ref NativeMethods.RECT _, IntPtr _) =>
        {
            var info = new NativeMethods.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                return true;
            }

            // DPI-tudatlan folyamatnál a Windows már 96 DPI-re virtualizálja a koordinátákat, rendszerszintűnél
            // egyetlen váltószám van; csak monitoronként tudatos (PerMonitorV2) folyamatnál kell a monitor saját DPI-je.
            double scale = awareness switch
            {
                NativeMethods.DPI_AWARENESS_PER_MONITOR_AWARE =>
                    NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0
                        ? dpiX / BaseDpi
                        : 1.0,
                NativeMethods.DPI_AWARENESS_SYSTEM_AWARE => systemScale,
                _ => 1.0,
            };

            result.Add(new SceneMonitor(ToRect(info.rcMonitor), ToRect(info.rcWork), scale));
            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return result;
    }

    private static int GetDpiAwareness()
    {
        try
        {
            return NativeMethods.GetAwarenessFromDpiAwarenessContext(NativeMethods.GetThreadDpiAwarenessContext());
        }
        catch (EntryPointNotFoundException)
        {
            // Windows 10 1607 előtt: rendszerszintű tudatosságot feltételezünk (a WPF alapértelmezése).
            return NativeMethods.DPI_AWARENESS_SYSTEM_AWARE;
        }
    }

    private static ScreenRect ToRect(NativeMethods.RECT r) => new(r.Left, r.Top, r.Right, r.Bottom);
}
