namespace OsEventEngine.Sources;

using MonkeyStack.Core.Events;
using OsEventEngine.Native;

/// <summary>
/// Processzorterhelés a <c>GetSystemTimes</c> két mintájának különbségéből:
/// <c>1 − ΔIdle / (ΔKernel + ΔUser)</c> (a kernelidő tartalmazza az üresjáratot).
/// A <c>PerformanceCounter</c>-rel szemben nincs bemelegedés és lokalizációs gond (plan.md, ADR-6).
/// </summary>
internal sealed class CpuSource : IMetricSource
{
    /// <summary>Ennél rövidebb mérési ablak zajos (pl. egy <c>TriggerNow</c> közvetlenül az előző minta után).</summary>
    private static readonly TimeSpan MinimumWindow = TimeSpan.FromMilliseconds(200);

    private readonly Func<SystemTimes> _read;
    private readonly ulong _minimumTotalTicks;
    private SystemTimes? _previous;

    /// <param name="read">A rendszeridők forrása; tesztekben cserélhető.</param>
    /// <param name="processorCount">Logikai processzorok száma (a rendszeridők összesítve jönnek); tesztekben cserélhető.</param>
    public CpuSource(Func<SystemTimes>? read = null, int? processorCount = null)
    {
        _read = read ?? Win32System.GetSystemTimes;
        _minimumTotalTicks = (ulong)(MinimumWindow.Ticks * Math.Max(1, processorCount ?? Environment.ProcessorCount));
    }

    public Metric Metric => Metric.Cpu;

    /// <returns>
    /// Az előző értékadó minta óta mért terhelés. <c>null</c> az első hívásnál, és ha a mérési ablak még
    /// túl rövid; ilyenkor az alapállapot megmarad, és a következő hívás a teljes ablakot méri.
    /// </returns>
    public int? Sample()
    {
        var current = _read();
        if (_previous is not { } previous)
        {
            _previous = current;
            return null;
        }

        ulong idle = current.Idle - previous.Idle;
        ulong total = (current.Kernel - previous.Kernel) + (current.User - previous.User);
        if (total < _minimumTotalTicks)
        {
            return null;
        }

        _previous = current;
        double busy = 1.0 - ((double)idle / total);
        return (int)Math.Round(Math.Clamp(busy, 0.0, 1.0) * 100.0);
    }
}
