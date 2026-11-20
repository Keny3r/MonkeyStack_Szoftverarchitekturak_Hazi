namespace OsEventEngine;

using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;
using OsEventEngine.Pipeline;
using OsEventEngine.Simulation;
using OsEventEngine.Sources;
using OsEventEngine.State;

/// <summary>
/// Az OS esemény értesítő motor egyetlen belépési pontja (Facade + Composition Root).
/// Méri a rendszereseményeket (Wrapper Facade forrásokkal), a mintákat egy feldolgozási láncon
/// (Half-Sync/Half-Async + Interceptor) vezeti át, és a közös memóriabeli <see cref="OsEventNotifier"/>
/// <c>Notify…Changed</c> metódusaival küldi ki őket.
/// Terv: docs/os_event_engine/plan.md · Agent összefoglaló: docs/os_event_engine/AGENT_BRIEF.md
/// </summary>
/// <remarks>
/// Használat a hostban: létrehozás → feliratkozás a <see cref="Notifier"/>-re → <see cref="Start"/> → <see cref="Dispose"/>.
/// A <see cref="Simulation"/> még nincs megvalósítva (plan.md, M5).
/// </remarks>
public class OsEventMonitorService : IDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _timeProvider;
    private readonly Func<TimeProvider, IEnumerable<IMetricSource>> _sourceFactory;
    private readonly MetricSnapshotStore _snapshot = new();
    private readonly object _lifecycle = new();

    private OsEventEngineOptions _options;
    private IPollingSettingsProvider? _pollingSettings;
    private CancellationTokenSource? _cts;
    private EventDispatcher? _dispatcher;
    private MonitorRepository? _monitors;
    private bool _disposed;

    /// <param name="notifier">A közös értesítő; ha <c>null</c>, saját példányt hoz létre (standalone futás).</param>
    /// <param name="options">Monitorozási beállítások; ha <c>null</c>, <see cref="OsEventEngineOptions.Default"/>.</param>
    /// <param name="timeProvider">Idő és időzítők forrása (tesztekben <c>FakeTimeProvider</c>); ha <c>null</c>, <see cref="TimeProvider.System"/>.</param>
    /// <remarks>A konstruktor nem indít semmit, így a host konstruktorában is biztonságosan létrehozható.</remarks>
    public OsEventMonitorService(
        OsEventNotifier? notifier = null,
        OsEventEngineOptions? options = null,
        TimeProvider? timeProvider = null)
        : this(notifier, options, timeProvider, sourceFactory: null)
    {
    }

    /// <summary>Tesztekhez: a mérési források cserélhetők (natív hívás nélküli futás).</summary>
    internal OsEventMonitorService(
        OsEventNotifier? notifier,
        OsEventEngineOptions? options,
        TimeProvider? timeProvider,
        Func<TimeProvider, IEnumerable<IMetricSource>>? sourceFactory)
    {
        Notifier = notifier ?? new OsEventNotifier();
        _options = options ?? OsEventEngineOptions.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _sourceFactory = sourceFactory ?? CreateLiveSources;
    }

    /// <summary>
    /// A közös értesítő. Erre iratkozik fel a <c>CharacterEventCoordinator</c> (<see cref="IOsEventNotifier"/>-ként).
    /// A <c>Notify…Changed</c> metódusait konvenció szerint csak ez a szolgáltatás hívja.
    /// </summary>
    public OsEventNotifier Notifier { get; }

    /// <summary>A jelenleg érvényes beállítások.</summary>
    public OsEventEngineOptions Options => Volatile.Read(ref _options);

    /// <summary>Igaz, ha a <see cref="Start"/> lefutott és a <see cref="Dispose"/> még nem.</summary>
    public bool IsRunning
    {
        get { lock (_lifecycle) { return _monitors is not null && !_disposed; } }
    }

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
    /// Elindítja a monitorokat és a láncot. Utána minden elérhető metrikára egy kezdeti esemény megy ki
    /// (a CPU-ra az első periódus után, mert két minta kell hozzá), majd csak változáskor.
    /// Az események sorosítva jönnek; ha volt <see cref="SynchronizationContext"/>, azon.
    /// </summary>
    /// <param name="context">Ahová az eseményeket küldjük; ha <c>null</c>, a hívó <see cref="SynchronizationContext.Current"/>-je (WPF: UI szál).</param>
    /// <exception cref="InvalidOperationException">Ha már fut.</exception>
    /// <exception cref="ObjectDisposedException">A <see cref="Dispose"/> után.</exception>
    public void Start(SynchronizationContext? context = null)
    {
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_monitors is not null)
            {
                throw new InvalidOperationException("Az OsEventMonitorService már fut.");
            }

            _cts = new CancellationTokenSource();
            var chain = new InterceptorChain(
            [
                new SmoothingInterceptor(() => Options.EnableSmoothing),
                new ChangeFilterInterceptor(metric => Options.For(metric).Deadband),
                new SnapshotInterceptor(_snapshot),
                new LoggingInterceptor(() => Options.EnableLogging),
            ]);

            _dispatcher = new EventDispatcher(chain, Notifier, context ?? SynchronizationContext.Current);
            _dispatcher.Start(_cts.Token);

            _monitors = new MonitorRepository(_sourceFactory(_timeProvider), _dispatcher.Writer, _timeProvider);
            _monitors.Apply(Options);
        }
    }

    /// <summary>
    /// Új beállítások alkalmazása újraindítás nélkül (Component Configurator): intervallumváltás → <c>Init</c>,
    /// letiltás → <c>Suspend</c>, engedélyezés → <c>Resume</c>. A <see cref="Start"/> előtt is hívható.
    /// </summary>
    public void Reconfigure(OsEventEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Volatile.Write(ref _options, options);
            _monitors?.Apply(options);
        }
    }

    /// <summary>
    /// A mintavételi beállításokat ettől a szolgáltatótól veszi (a Konfigurációs motor valósítja meg, a host adja át),
    /// és a <see cref="IPollingSettingsProvider.PollingSettingsChanged"/> eseményre újrakonfigurál.
    /// <c>null</c> intervallum = a metrika letiltva. A <see cref="Start"/> előtt és után is hívható.
    /// </summary>
    public void UsePollingSettings(IPollingSettingsProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pollingSettings is not null)
            {
                _pollingSettings.PollingSettingsChanged -= OnPollingSettingsChanged;
            }

            _pollingSettings = provider;
            provider.PollingSettingsChanged += OnPollingSettingsChanged;
        }

        ApplyPollingSettings(provider);
    }

    /// <summary>A metrikák legutóbb kiküldött értékei, másolatként (az OS Mock View és a tesztek számára).</summary>
    public OsEventDataRegistry GetSnapshot() => _snapshot.ToRegistry();

    /// <summary>Egy monitor állapota (OS Mock View "Monitor" oszlop); a <see cref="Start"/> előtt <see cref="MonitorStatus.Stopped"/>.</summary>
    public MonitorStatus GetMonitorStatus(Metric metric)
    {
        lock (_lifecycle)
        {
            return _monitors is not null && _monitors.TryGet(metric, out var monitor) ? monitor.Status : MonitorStatus.Stopped;
        }
    }

    /// <summary>Egy monitor ember által olvasható állapota, pl. <c>"Running @ 1000 ms"</c>.</summary>
    public string GetMonitorInfo(Metric metric)
    {
        lock (_lifecycle)
        {
            return _monitors is not null && _monitors.TryGet(metric, out var monitor) ? monitor.Info() : MonitorStatus.Stopped.ToString();
        }
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
    /// Az eredeti váz példája az élő mérésre. Elavult: az élő mérést a motor maga végzi.
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
    /// A UI szálról is biztonságosan hívható: a belső ciklusok nem igénylik a UI szálat.
    /// </summary>
    public void Dispose()
    {
        Task stopping;
        lock (_lifecycle)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_pollingSettings is not null)
            {
                _pollingSettings.PollingSettingsChanged -= OnPollingSettingsChanged;
                _pollingSettings = null;
            }

            var monitorsStopped = _monitors?.FiniAllAsync() ?? Task.CompletedTask;
            _cts?.Cancel();
            _dispatcher?.Writer.TryComplete();
            stopping = Task.WhenAll(monitorsStopped, _dispatcher?.Completion ?? Task.CompletedTask);
            _monitors = null;
        }

        try
        {
            stopping.Wait(StopTimeout);
        }
        catch (AggregateException)
        {
            // A leállítás nem dobhat kivételt.
        }

        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static IEnumerable<IMetricSource> CreateLiveSources(TimeProvider timeProvider) =>
    [
        new CpuSource(),
        new MemorySource(),
        new DiskSource(),
        new BatterySource(),
        new WindowCountSource(),
        new TimeSource(timeProvider),
        new InactivitySource(),
    ];

    private void OnPollingSettingsChanged(object? sender, EventArgs e)
    {
        IPollingSettingsProvider? provider;
        lock (_lifecycle)
        {
            provider = _pollingSettings;
        }

        if (provider is null)
        {
            return;
        }

        try
        {
            ApplyPollingSettings(provider);
        }
        catch (ObjectDisposedException)
        {
            // Az esemény a leállítással egy időben érkezett.
        }
    }

    private void ApplyPollingSettings(IPollingSettingsProvider provider)
    {
        var current = Options;
        var monitors = MetricCatalog.All.ToDictionary(
            metric => metric,
            metric =>
            {
                var options = current.For(metric);
                return provider.GetPollingInterval(metric) is TimeSpan interval
                    ? options with { Enabled = true, Interval = interval }
                    : options with { Enabled = false };
            });

        Reconfigure(current with { Monitors = monitors });
    }
}
