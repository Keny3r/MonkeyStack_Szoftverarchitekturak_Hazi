namespace OsEventEngine.Monitoring;

using MonkeyStack.Core.Events;
using OsEventEngine.Simulation;

/// <summary>
/// A metrikák értéktartományai, alapértelmezett mintavételezése és UI-leírói egy helyen.
/// A tartományok és az intervallumok megegyeznek a Konfigurációs motor <c>ConfigurationLimits</c> / <c>MetricPolling</c>
/// értékeivel (docs/configuration_engine/schema.md, "MetricPolling" és "Metrikák és értéktartományok").
/// TODO: a merge után közösen a Core-ba (a <see cref="Metric"/> mellé), hogy egy helyen éljenek (PR #3, Levente).
/// </summary>
public static class MetricCatalog
{
    /// <summary>A mintavételi intervallum megengedett tartománya (ms), mint a <c>ConfigurationLimits.PollingIntervalMs</c>.</summary>
    public const int MinIntervalMs = 250;

    /// <summary>A mintavételi intervallum felső határa (ms).</summary>
    public const int MaxIntervalMs = 60_000;

    /// <summary>Az összes metrika, a felsorolás sorrendjében.</summary>
    public static IReadOnlyList<Metric> All { get; } = Enum.GetValues<Metric>();

    /// <summary>A metrika érvényes értéktartománya (zárt intervallum). A források ide vágják az értéket.</summary>
    public static (int Min, int Max) Range(Metric metric) => metric switch
    {
        Metric.Cpu or Metric.Memory or Metric.Disk or Metric.Battery => (0, 100),
        Metric.WindowCount => (0, 1000),
        Metric.Time => (0, 1439),
        Metric.Inactivity => (0, 1440),
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null)
    };

    /// <summary>Az értéket a metrika tartományára vágja.</summary>
    public static int Clamp(Metric metric, int value)
    {
        var (min, max) = Range(metric);
        return Math.Clamp(value, min, max);
    }

    /// <summary>Alapértelmezett mintavételi intervallum (a <c>MetricPolling</c> alapértékei).</summary>
    public static TimeSpan DefaultInterval(Metric metric) => TimeSpan.FromMilliseconds(metric switch
    {
        Metric.Cpu or Metric.Memory => 1_000,
        Metric.WindowCount or Metric.Inactivity => 2_000,
        Metric.Disk or Metric.Battery or Metric.Time => 30_000,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null)
    });

    /// <summary>
    /// A tesztpanel sorai. A csúszka-tartomány ott szűkebb lehet a teljes tartománynál (ablakszám, inaktivitás),
    /// hogy a gyakran tesztelt értékek könnyen beállíthatók legyenek.
    /// </summary>
    public static IReadOnlyList<MetricDescriptor> Descriptors { get; } =
    [
        new(Metric.Cpu, "CPU", 0, 100, "%"),
        new(Metric.Memory, "Memória", 0, 100, "%"),
        new(Metric.Disk, "Háttértár", 0, 100, "%"),
        new(Metric.Battery, "Akkumulátor", 0, 100, "%"),
        new(Metric.WindowCount, "Ablakszám", 0, 50, "db"),
        new(Metric.Time, "Idő", 0, 1439, "perc éjfél óta"),
        new(Metric.Inactivity, "Inaktivitás", 0, 120, "perc"),
    ];
}
