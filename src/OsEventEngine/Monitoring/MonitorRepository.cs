namespace OsEventEngine.Monitoring;

using System.Threading.Channels;
using MonkeyStack.Core.Events;
using OsEventEngine.Sources;

/// <summary>
/// A monitorok nyilvántartása (Component Configurator repository). Egy helyen alkalmazza
/// a beállításokat az összes monitorra, és egy helyen állítja le őket.
/// </summary>
internal sealed class MonitorRepository
{
    private readonly Dictionary<Metric, MetricMonitor> _monitors;

    public MonitorRepository(IEnumerable<IMetricSource> sources, ChannelWriter<MetricSample> output, TimeProvider timeProvider)
    {
        _monitors = sources.ToDictionary(s => s.Metric, s => new MetricMonitor(s, output, timeProvider));
    }

    public IReadOnlyCollection<MetricMonitor> Monitors => _monitors.Values;

    public bool TryGet(Metric metric, out MetricMonitor monitor) => _monitors.TryGetValue(metric, out monitor!);

    /// <summary>Az új beállítások alkalmazása minden monitorra; az első hívás el is indítja őket.</summary>
    public void Apply(OsEventEngineOptions options)
    {
        foreach (var monitor in _monitors.Values)
        {
            monitor.Init(options.For(monitor.Metric));
        }
    }

    /// <summary>Minden monitor leállítása; a visszaadott taszk a ciklusok befejeződésekor teljesül.</summary>
    public Task FiniAllAsync()
    {
        foreach (var monitor in _monitors.Values)
        {
            monitor.Fini();
        }

        return Task.WhenAll(_monitors.Values.Select(m => m.Completion));
    }
}
