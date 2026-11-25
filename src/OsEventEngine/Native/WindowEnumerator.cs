namespace OsEventEngine.Native;

/// <summary>
/// Egy felső szintű ablak adatai, ahogy az <see cref="WindowEnumerator"/> látja.
/// </summary>
/// <param name="Handle">Az ablak azonosítója.</param>
/// <param name="IsMinimized">Minimalizált-e.</param>
internal readonly record struct TopLevelWindow(IntPtr Handle, bool IsMinimized);

/// <summary>
/// A felhasználó számára látható ("Alt-Tab") felső szintű ablakok felsorolása Z-sorrendben, elölről hátra
/// (Wrapper Facade az <c>EnumWindows</c> és a DWM fölött). A saját folyamat ablakait kihagyja, hogy a
/// screenmate, a konfigurációs ablak és az OS Mock View ne számítson.
/// A platform-szolgáltató (docs/os_event_engine/platform_provider_proposal.md, M11) ugyanezt a szűrést használja
/// (<see cref="Win32DesktopScanner"/>), így az ablakszám és a járható ablakok halmaza konzisztens.
/// </summary>
internal static class WindowEnumerator
{
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    public static IReadOnlyList<TopLevelWindow> GetUserWindows()
    {
        var result = new List<TopLevelWindow>();
        NativeMethods.EnumWindowsProc callback = (hWnd, _) =>
        {
            if (IsUserWindow(hWnd))
            {
                result.Add(new TopLevelWindow(hWnd, NativeMethods.IsIconic(hWnd)));
            }

            return true;
        };

        NativeMethods.EnumWindows(callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return result;
    }

    internal static bool IsUserWindow(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd))
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
        if (processId == OwnProcessId)
        {
            return false;
        }

        if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
        {
            return false;
        }

        long exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        bool isToolWindow = (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0;
        bool isAppWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;
        if (isToolWindow && !isAppWindow)
        {
            return false;
        }

        // Másik virtuális asztalon lévő vagy felfüggesztett (UWP) ablak: látható, de "elrejtett".
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
            && cloaked != 0)
        {
            return false;
        }

        return NativeMethods.GetWindowTextLength(hWnd) > 0;
    }
}
