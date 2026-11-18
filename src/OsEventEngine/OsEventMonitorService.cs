namespace OsEventEngine;

using MonkeyStack.Core.Events;

/// <summary>
/// Az OS eseményeket monitorozó háttérszolgáltatás váza (Wrapper Facade / Active Object minta).
/// A valódi OS metrikákat gyűjti, és a közös memóriabeli OsEventNotifier példányon keresztül
/// értesíti a rendszert. Támogatja a Mock / Override módot, hogy a tesztelői View csúszkáit
/// ne írja felül a háttérben futó lekérdezés.
/// </summary>
public class OsEventMonitorService
{
    public OsEventNotifier Notifier { get; }

    /// <summary>
    /// Ha igaz, a valódi OS polling nem írja felül a tesztelő (Mock) View által beállított értékeket.
    /// </summary>
    public bool IsMockModeEnabled { get; set; }

    public OsEventMonitorService(OsEventNotifier? notifier = null)
    {
        Notifier = notifier ?? new OsEventNotifier();
    }

    /// <summary>
    /// Példa a valódi OS monitorozó szálak számára: csak akkor küld értesítést, ha nincs bekapcsolva a Mock mód.
    /// (Az OS Event csapat ugyanígy hívhatja a Memory, Disk, Battery, WindowCount, Time, Inactivity metrikákra is.)
    /// </summary>
    public void ReportRealCpuUsage(int usagePercent)
    {
        if (!IsMockModeEnabled)
        {
            Notifier.NotifyCpuChanged(new CpuEventData { UsagePercent = usagePercent });
        }
    }

    /// <summary>
    /// Példa a Mock / Tesztelő View csúszkái számára: bekapcsolja a Mock módot és közvetlenül értesíti a rendszert.
    /// </summary>
    public void ReportMockCpuUsage(int usagePercent)
    {
        IsMockModeEnabled = true;
        Notifier.NotifyCpuChanged(new CpuEventData { UsagePercent = usagePercent });
    }
}
