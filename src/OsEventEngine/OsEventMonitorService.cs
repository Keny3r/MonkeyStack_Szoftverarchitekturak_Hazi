namespace OsEventEngine;

using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;
using OsEventEngine.Simulation;

/// <summary>
/// Az OS esemény értesítő motor egyetlen belépési pontja (Facade + Composition Root).
/// Méri a rendszereseményeket (Wrapper Facade forrásokkal), a mintákat egy feldolgozási láncon
/// (Half-Sync/Half-Async + Interceptor) vezeti át, és a közös memóriabeli <see cref="OsEventNotifier"/>
/// <c>Notify…Changed</c> metódusaival küldi ki őket.
/// Terv: docs/os_event_engine/plan.md · Agent összefoglaló: docs/os_event_engine/AGENT_BRIEF.md
/// </summary>
/// <remarks>
/// Állapot: a publikus szerződés kész, a mérés és a lánc még nincs megvalósítva (plan.md, M1–M6).
/// A <see cref="NotImplementedException"/>-t dobó tagok addig nem hívhatók.
/// Használat a hostban: létrehozás → feliratkozás a <see cref="Notifier"/>-re → <see cref="Start"/> → <see cref="Dispose"/>.
/// </remarks>
public class OsEventMonitorService : IDisposable
{
    private readonly TimeProvider _timeProvider;
    private OsEventEngineOptions _options;

    /// <param name="notifier">A közös értesítő; ha <c>null</c>, saját példányt hoz létre (standalone futás).</param>
    /// <param name="options">Monitorozási beállítások; ha <c>null</c>, <see cref="OsEventEngineOptions.Default"/>.</param>
    /// <param name="timeProvider">Idő és időzítők forrása (tesztekben <c>FakeTimeProvider</c>); ha <c>null</c>, <see cref="TimeProvider.System"/>.</param>
    /// <remarks>A konstruktor nem indít semmit, így a host konstruktorában is biztonságosan létrehozható.</remarks>
    public OsEventMonitorService(
        OsEventNotifier? notifier = null,
        OsEventEngineOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        Notifier = notifier ?? new OsEventNotifier();
        _options = options ?? OsEventEngineOptions.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// A közös értesítő. Erre iratkozik fel a <c>CharacterEventCoordinator</c> (<see cref="IOsEventNotifier"/>-ként).
    /// A <c>Notify…Changed</c> metódusait konvenció szerint csak ez a szolgáltatás hívja.
    /// </summary>
    public OsEventNotifier Notifier { get; }

    /// <summary>A jelenleg érvényes beállítások.</summary>
    public OsEventEngineOptions Options => _options;

    /// <summary>Igaz, ha a <see cref="Start"/> lefutott és a <see cref="Dispose"/> még nem.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// A szimuláció vezérlése (OS Mock View csúszkái, más komponensek tesztjei).
    /// </summary>
    /// <exception cref="NotImplementedException">M5 előtt.</exception>
    public ISimulationControl Simulation => throw new NotImplementedException("M5: SimulationController (plan.md 5.1)");

    /// <summary>
    /// Kényelmi kapcsoló a metrikánkénti szimuláció fölött.
    /// Tervezett viselkedés (M5): <c>true</c> → minden metrika szimulált módba a jelenlegi értékkel;
    /// <c>false</c> → <see cref="ISimulationControl.ResetAll"/>. Olvasva: igaz, ha bármelyik metrika szimulált.
    /// Addig egyszerű jelző, ahogy az eredeti vázban.
    /// </summary>
    public bool IsMockModeEnabled { get; set; }

    /// <summary>
    /// Elindítja a monitorokat és a láncot. Utána minden elérhető metrikára egy kezdeti esemény megy ki,
    /// majd csak változáskor. Az események sorosítva jönnek; ha volt <see cref="SynchronizationContext"/>, azon.
    /// </summary>
    /// <param name="context">Ahová az eseményeket küldjük; ha <c>null</c>, a hívó <see cref="SynchronizationContext.Current"/>-je (WPF: UI szál).</param>
    /// <exception cref="NotImplementedException">M4 előtt.</exception>
    public void Start(SynchronizationContext? context = null)
    {
        throw new NotImplementedException("M4: monitorok, lánc és kiküldés indítása (plan.md 6.2)");
    }

    /// <summary>
    /// Új beállítások alkalmazása újraindítás nélkül (Component Configurator): intervallumváltás → <c>Init</c>,
    /// letiltás → <c>Suspend</c>, engedélyezés → <c>Resume</c>.
    /// </summary>
    /// <exception cref="NotImplementedException">M4 előtt.</exception>
    public void Reconfigure(OsEventEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        throw new NotImplementedException("M4: MonitorRepository.Reconfigure (plan.md 3.1)");
    }

    /// <summary>A metrikák legutóbb kiküldött értékei, másolatként (az OS Mock View és a tesztek számára).</summary>
    /// <exception cref="NotImplementedException">M3 előtt.</exception>
    public OsEventDataRegistry GetSnapshot()
    {
        throw new NotImplementedException("M3: MetricSnapshotStore (plan.md 3.3)");
    }

    /// <summary>Egy monitor állapota (OS Mock View "Monitor" oszlop).</summary>
    /// <exception cref="NotImplementedException">M2 előtt.</exception>
    public MonitorStatus GetMonitorStatus(Metric metric)
    {
        throw new NotImplementedException("M2: MetricMonitor (plan.md 3.1)");
    }

    /// <summary>
    /// Rövidítés a csúszkákhoz: CPU szimulált értéke.
    /// Tervezett viselkedés (M5): <c>Simulation.SetSimulatedValue(Metric.Cpu, value)</c>, az esemény a láncon át,
    /// &lt; 100 ms-on belül megy ki. Addig az eredeti váz szerint közvetlenül értesít.
    /// </summary>
    public void ReportMockCpuUsage(int usagePercent)
    {
        IsMockModeEnabled = true;
        Notifier.NotifyCpuChanged(new CpuEventData { UsagePercent = MetricCatalog.Clamp(Metric.Cpu, usagePercent) });
    }

    /// <summary>
    /// Az eredeti váz példája az élő mérésre. Elavult: az élő mérést a motor maga végzi (M1–M4).
    /// </summary>
    [Obsolete("Az élő mérést az OsEventMonitorService maga végzi (plan.md 5.1). Teszteléshez: Simulation.")]
    public void ReportRealCpuUsage(int usagePercent)
    {
        if (!IsMockModeEnabled)
        {
            Notifier.NotifyCpuChanged(new CpuEventData { UsagePercent = MetricCatalog.Clamp(Metric.Cpu, usagePercent) });
        }
    }

    /// <summary>
    /// Leállítja a monitorokat és felszabadítja az erőforrásokat (legfeljebb ~1 s). Többször is hívható, nem dob kivételt.
    /// </summary>
    public void Dispose()
    {
        // M4: CancellationTokenSource.Cancel, ciklusok megvárása (ConfigureAwait(false)), Channel lezárása, hookok felszabadítása.
        IsRunning = false;
        GC.SuppressFinalize(this);
    }
}
