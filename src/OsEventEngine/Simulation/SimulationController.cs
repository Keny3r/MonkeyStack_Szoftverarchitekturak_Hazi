namespace OsEventEngine.Simulation;

using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;

/// <summary>
/// Az <see cref="ISimulationControl"/> megvalósítása. A hívás (pl. a UI csúszkája) csak beállítja a forrás
/// módját és értékét, majd <see cref="IMetricMonitor.TriggerNow"/>-val kér egy mintát a monitor saját
/// ciklusától (Active Object jelleg: a hívás és a végrehajtás szétválik). A monitor ébresztő csatornája
/// egy elemű, ezért a gyors csúszkahúzás magától összevonódik, külön throttle nem kell.
/// </summary>
internal sealed class SimulationController : ISimulationControl
{
    private readonly IReadOnlyDictionary<Metric, SwitchableSource> _sources;
    private readonly Func<Metric, IMetricMonitor?> _findMonitor;

    /// <param name="sources">A szimulálható források metrikánként.</param>
    /// <param name="findMonitor">A metrika futó monitora, ha van (a <c>Start</c> előtt <c>null</c>).</param>
    public SimulationController(IReadOnlyDictionary<Metric, SwitchableSource> sources, Func<Metric, IMetricMonitor?> findMonitor)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _findMonitor = findMonitor ?? throw new ArgumentNullException(nameof(findMonitor));
    }

    public IReadOnlyList<MetricDescriptor> Metrics { get; } = MetricCatalog.Descriptors;

    public SourceMode GetMode(Metric metric) => Get(metric).Mode;

    public void SetMode(Metric metric, SourceMode mode)
    {
        var source = Get(metric);
        if (source.Mode == mode)
        {
            return;
        }

        source.Mode = mode;
        _findMonitor(metric)?.TriggerNow();
    }

    public void SetSimulatedValue(Metric metric, int value)
    {
        var source = Get(metric);

        // Előbb a mód: élőből váltva az az utolsó élő értéket állítja be, amit utána felülírunk.
        source.Mode = SourceMode.Simulated;
        source.SimulatedValue = value;
        _findMonitor(metric)?.TriggerNow();
    }

    public void ApplyPreset(IReadOnlyDictionary<Metric, int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var (metric, value) in values)
        {
            SetSimulatedValue(metric, value);
        }
    }

    public void ResetAll()
    {
        foreach (var metric in _sources.Keys)
        {
            SetMode(metric, SourceMode.Live);
        }
    }

    /// <summary>Igaz, ha legalább egy metrika szimulált.</summary>
    public bool AnySimulated => _sources.Values.Any(s => s.Mode == SourceMode.Simulated);

    /// <summary>Minden metrika szimulált módba, a jelenlegi (utolsó élő) értékkel.</summary>
    public void SimulateAll()
    {
        foreach (var metric in _sources.Keys)
        {
            SetMode(metric, SourceMode.Simulated);
        }
    }

    /// <summary>Az utolsó élő mérés (az OS Mock View "Élő" oszlopa).</summary>
    public int? GetLastLiveValue(Metric metric) => Get(metric).LastLiveValue;

    /// <summary>A beállított szimulált érték (akkor is, ha a metrika épp élő módban van).</summary>
    public int GetSimulatedValue(Metric metric) => Get(metric).SimulatedValue;

    private SwitchableSource Get(Metric metric) =>
        _sources.TryGetValue(metric, out var source)
            ? source
            : throw new ArgumentException($"A(z) {metric} metrika nem szimulálható.", nameof(metric));
}
