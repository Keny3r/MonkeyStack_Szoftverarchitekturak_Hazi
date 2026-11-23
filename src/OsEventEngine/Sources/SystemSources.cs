namespace OsEventEngine.Sources;

using System.IO;
using MonkeyStack.Core.Events;
using OsEventEngine.Native;

/// <summary>
/// Memóriahasználat a <c>GlobalMemoryStatusEx</c> <c>dwMemoryLoad</c> mezőjéből.
/// </summary>
internal sealed class MemorySource(Func<int>? read = null) : IMetricSource
{
    private readonly Func<int> _read = read ?? Win32System.GetMemoryLoadPercent;

    public Metric Metric => Metric.Memory;

    public int? Sample() => _read();
}

/// <summary>
/// A rendszermeghajtó telítettsége: <c>(Teljes − Szabad) / Teljes</c>.
/// </summary>
internal sealed class DiskSource : IMetricSource
{
    private readonly Func<(long Total, long Free)> _read;

    /// <param name="read">Teljes és szabad méret bájtban; ha <c>null</c>, a rendszermeghajtó (<see cref="DriveInfo"/>).</param>
    public DiskSource(Func<(long Total, long Free)>? read = null)
    {
        _read = read ?? ReadSystemDrive;
    }

    public Metric Metric => Metric.Disk;

    public int? Sample()
    {
        var (total, free) = _read();
        if (total <= 0)
        {
            return null;
        }

        return (int)Math.Round((total - free) * 100.0 / total);
    }

    private static (long Total, long Free) ReadSystemDrive()
    {
        string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var drive = new DriveInfo(root);
        return (drive.TotalSize, drive.TotalFreeSpace);
    }
}

/// <summary>
/// Akkumulátortöltöttség a <c>GetSystemPowerStatus</c>-ból. Akkumulátor nélküli gépen <c>null</c>,
/// így erre a metrikára nem megy esemény, és a fogyasztó registryjében <c>Battery == null</c> marad.
/// </summary>
internal sealed class BatterySource(Func<int?>? read = null) : IMetricSource
{
    private readonly Func<int?> _read = read ?? Win32System.GetBatteryPercent;

    public Metric Metric => Metric.Battery;

    public int? Sample() => _read();
}

/// <summary>
/// A felhasználó számára látható ("Alt-Tab") ablakok száma, a saját folyamat ablakai nélkül.
/// </summary>
internal sealed class WindowCountSource(Func<int>? read = null) : IMetricSource
{
    private readonly Func<int> _read = read ?? (() => WindowEnumerator.GetUserWindows().Count);

    public Metric Metric => Metric.WindowCount;

    public int? Sample() => _read();
}

/// <summary>
/// Helyi idő, éjfél óta eltelt percekben (0–1439). A percváltást a változásszűrő ismeri fel.
/// </summary>
internal sealed class TimeSource(TimeProvider timeProvider) : IMetricSource
{
    public Metric Metric => Metric.Time;

    public int? Sample()
    {
        var now = timeProvider.GetLocalNow();
        return (now.Hour * 60) + now.Minute;
    }
}

/// <summary>
/// Felhasználói inaktivitás percben a <c>GetLastInputInfo</c> alapján.
/// </summary>
internal sealed class InactivitySource(Func<uint>? readIdleMilliseconds = null) : IMetricSource
{
    private readonly Func<uint> _read = readIdleMilliseconds ?? Win32System.GetIdleMilliseconds;

    public Metric Metric => Metric.Inactivity;

    public int? Sample() => (int)(_read() / 60_000u);
}
