namespace OsEventEngine.State;

using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;

/// <summary>
/// A metrikák legutóbb kiküldött értékei (Monitor Object). A feldolgozási lánc írja (fogyasztó szál),
/// az OS Mock View és a <c>GetSnapshot</c> olvassa (UI szál).
/// Thread-Safe Interface: a publikus metódusok lockolnak (Scoped Locking: <c>lock</c> blokk),
/// és csak lock nélküli privát segédmetódusokat hívnak.
/// </summary>
internal sealed class MetricSnapshotStore
{
    private readonly object _sync = new();
    private readonly Dictionary<Metric, MetricSample> _latest = [];

    public void Update(in MetricSample sample)
    {
        lock (_sync)
        {
            _latest[sample.Metric] = sample;
        }
    }

    public bool TryGet(Metric metric, out MetricSample sample)
    {
        lock (_sync)
        {
            return _latest.TryGetValue(metric, out sample);
        }
    }

    /// <summary>A jelenlegi értékek másolata a Core adatmodelljében; minden hívás új példányokat ad.</summary>
    public OsEventDataRegistry ToRegistry()
    {
        lock (_sync)
        {
            return ToRegistryUnlocked();
        }
    }

    private OsEventDataRegistry ToRegistryUnlocked()
    {
        var registry = new OsEventDataRegistry();
        foreach (var sample in _latest.Values)
        {
            EventDataMapper.Apply(registry, sample);
        }

        return registry;
    }
}

/// <summary>
/// A belső <see cref="MetricSample"/> és a Core <c>*EventData</c> modellek közötti leképezés egy helyen.
/// </summary>
internal static class EventDataMapper
{
    public static void Apply(OsEventDataRegistry registry, in MetricSample s)
    {
        switch (s.Metric)
        {
            case Metric.Cpu: registry.Cpu = new CpuEventData { UsagePercent = s.Value }; break;
            case Metric.Memory: registry.Memory = new MemoryEventData { UsagePercent = s.Value }; break;
            case Metric.Disk: registry.Disk = new DiskEventData { UsagePercent = s.Value }; break;
            case Metric.Battery: registry.Battery = new BatteryEventData { ChargePercent = s.Value }; break;
            case Metric.WindowCount: registry.WindowCount = new WindowCountEventData { WindowCount = s.Value }; break;
            case Metric.Time: registry.Time = new TimeEventData { Hour = s.Value / 60, Minute = s.Value % 60 }; break;
            case Metric.Inactivity: registry.Inactivity = new InactivityEventData { InactivityMinutes = s.Value }; break;
            default: throw new ArgumentOutOfRangeException(nameof(s), s.Metric, null);
        }
    }

    /// <summary>A minta kiküldése a megfelelő <c>Notify…Changed</c> hívással, mindig új adatpéldánnyal.</summary>
    public static void Publish(OsEventNotifier notifier, in MetricSample s)
    {
        switch (s.Metric)
        {
            case Metric.Cpu: notifier.NotifyCpuChanged(new CpuEventData { UsagePercent = s.Value }); break;
            case Metric.Memory: notifier.NotifyMemoryChanged(new MemoryEventData { UsagePercent = s.Value }); break;
            case Metric.Disk: notifier.NotifyDiskChanged(new DiskEventData { UsagePercent = s.Value }); break;
            case Metric.Battery: notifier.NotifyBatteryChanged(new BatteryEventData { ChargePercent = s.Value }); break;
            case Metric.WindowCount: notifier.NotifyWindowCountChanged(new WindowCountEventData { WindowCount = s.Value }); break;
            case Metric.Time: notifier.NotifyTimeChanged(new TimeEventData { Hour = s.Value / 60, Minute = s.Value % 60 }); break;
            case Metric.Inactivity: notifier.NotifyInactivityChanged(new InactivityEventData { InactivityMinutes = s.Value }); break;
            default: throw new ArgumentOutOfRangeException(nameof(s), s.Metric, null);
        }
    }
}
