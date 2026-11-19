namespace ConfigurationEngine.Model;

using MonkeyStack.Core.Events;

/// <summary>
/// A konfiguráció alapértékei (schema.md, "CharacterSettings" és "MetricPolling").
/// Ezekkel jön létre az új adatbázis, és ezek érvényesek egy hiányzó vagy hibás sor helyett.
/// </summary>
public static class ConfigurationDefaults
{
    /// <summary>Az alapértelmezett karakter beállítások (új példány).</summary>
    public static CharacterSettings CreateCharacterSettings() => new();

    /// <summary>A metrika alapértelmezett lekérdezési gyakorisága ezredmásodpercben.</summary>
    public static int PollingIntervalMs(Metric metric) => metric switch
    {
        Metric.Cpu or Metric.Memory => 1_000,
        Metric.Inactivity or Metric.WindowCount => 2_000,
        Metric.Disk or Metric.Battery or Metric.Time => 30_000,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null)
    };

    /// <summary>Az alapértelmezett mintavételezési beállítás egy metrikához (új példány).</summary>
    public static MetricPolling CreateMetricPolling(Metric metric) => new()
    {
        Metric = metric,
        IsEnabled = true,
        IntervalMs = PollingIntervalMs(metric)
    };

    /// <summary>Az összes metrika alapértelmezett mintavételezési beállítása, a felsorolás sorrendjében.</summary>
    public static List<MetricPolling> CreateMetricPollings() =>
        Enum.GetValues<Metric>().Select(CreateMetricPolling).ToList();
}
