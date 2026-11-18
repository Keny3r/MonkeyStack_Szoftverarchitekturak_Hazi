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

    // TODO (OS Event csapat): Háttérszálú rendszeresemény-figyelők (CPU, RAM, Disk, Battery,
    // WindowCount, Time, Inactivity) implementálása, amelyek (!IsMockModeEnabled esetén)
    // meghívják a Notifier.Notify...Changed(...) metódusokat.
}
