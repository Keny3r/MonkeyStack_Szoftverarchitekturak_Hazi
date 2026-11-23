namespace OsEventEngine.Native;

using System.ComponentModel;
using System.Runtime.InteropServices;

/// <summary>
/// Rendszeridők a CPU-terhelés számításához (100 ns-os egységekben, a rendszerindítás óta).
/// A kernelidő az üresjárati időt is tartalmazza.
/// </summary>
internal readonly record struct SystemTimes(ulong Idle, ulong Kernel, ulong User);

/// <summary>
/// Típusos burkoló a rendszerszintű Win32 lekérdezések fölött (Wrapper Facade).
/// Hibánál <see cref="Win32Exception"/>-t dob, amit a monitor kezel.
/// </summary>
internal static class Win32System
{
    public static SystemTimes GetSystemTimes()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetSystemTimes");
        }

        return new SystemTimes(idle.Value, kernel.Value, user.Value);
    }

    /// <summary>A fizikai memória kihasználtsága százalékban (0–100).</summary>
    public static int GetMemoryLoadPercent()
    {
        var status = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GlobalMemoryStatusEx");
        }

        return (int)status.dwMemoryLoad;
    }

    /// <summary>Az akkumulátor töltöttsége százalékban, vagy <c>null</c>, ha nincs akkumulátor vagy ismeretlen.</summary>
    public static int? GetBatteryPercent()
    {
        if (!NativeMethods.GetSystemPowerStatus(out var status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetSystemPowerStatus");
        }

        bool noBattery = status.BatteryFlag == NativeMethods.BATTERY_FLAG_UNKNOWN
            || (status.BatteryFlag & NativeMethods.BATTERY_FLAG_NO_BATTERY) != 0
            || status.BatteryLifePercent == NativeMethods.BATTERY_PERCENT_UNKNOWN;

        return noBattery ? null : status.BatteryLifePercent;
    }

    /// <summary>Az utolsó billentyűzet- vagy egéresemény óta eltelt idő ezredmásodpercben.</summary>
    public static uint GetIdleMilliseconds()
    {
        var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetLastInputInfo");
        }

        // Előjel nélküli kivonás: a 49,7 napos tick-túlcsordulásnál is helyes eredményt ad.
        return unchecked((uint)Environment.TickCount - info.dwTime);
    }
}
