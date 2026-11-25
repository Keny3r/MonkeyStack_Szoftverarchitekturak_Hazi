namespace OsEventEngine;

using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;

/// <summary>
/// Egy metrika monitorozási beállítása (Component Configurator: futás közben cserélhető).
/// Megfelel a Konfigurációs motor <c>MetricPolling</c> sorának (docs/configuration_engine/schema.md), kiegészítve a belső deadbanddel.
/// Integráláskor az <see cref="IPollingSettingsProvider"/> adja (null intervallum = <see cref="Enabled"/> = false).
/// </summary>
/// <param name="Enabled">Letiltott metrikát nem mérünk, és nem küldünk rá eseményt.</param>
/// <param name="Interval">Mintavételi periódus (<see cref="MetricCatalog.MinIntervalMs"/>–<see cref="MetricCatalog.MaxIntervalMs"/>).</param>
/// <param name="Deadband">Csak akkor megy ki esemény, ha |új − utolsó küldött| ≥ Deadband. Legalább 1.</param>
public sealed record MonitorOptions(bool Enabled, TimeSpan Interval, int Deadband = 1);

/// <summary>
/// Az OS motor teljes beállítása. Az <see cref="OsEventMonitorService.Reconfigure"/> újraindítás nélkül alkalmazza.
/// </summary>
public sealed record OsEventEngineOptions
{
    /// <summary>Metrikánkénti beállítások; a hiányzó metrika az alapértékkel fut.</summary>
    public IReadOnlyDictionary<Metric, MonitorOptions> Monitors { get; init; } = DefaultMonitors;

    /// <summary>Exponenciális mozgóátlag az élő CPU értékre, hogy a tüskék ne villogtassák az overlay-t.</summary>
    public bool EnableSmoothing { get; init; } = true;

    /// <summary>Minden kimenő esemény naplózása (Debug kimenet).</summary>
    public bool EnableLogging { get; init; }

    /// <summary>
    /// OS értesítések (Reactor, M9): ablak megjelenés / eltűnés, tápellátás, időváltás, ébredés esetén azonnali mintavétel
    /// a periódus kivárása nélkül. A pollozás ettől függetlenül megmarad biztonsági hálónak.
    /// </summary>
    public bool EnableOsNotifications { get; init; } = true;

    // A sorrend számít: a statikus mezők deklarációs sorrendben inicializálódnak, és a Default már a DefaultMonitors-t használja.

    /// <summary>Az alapértelmezett metrikánkénti beállítások.</summary>
    public static IReadOnlyDictionary<Metric, MonitorOptions> DefaultMonitors { get; } =
        MetricCatalog.All.ToDictionary(m => m, m => new MonitorOptions(true, MetricCatalog.DefaultInterval(m)));

    /// <summary>Alapértelmezett beállítás: minden metrika engedélyezve, <see cref="MetricCatalog.DefaultInterval"/> periódussal.</summary>
    public static OsEventEngineOptions Default { get; } = new();

    /// <summary>A metrika érvényes beállítása: ha nincs megadva, az alapérték; az intervallum a megengedett tartományra vágva.</summary>
    public MonitorOptions For(Metric metric)
    {
        var options = Monitors.TryGetValue(metric, out var o) ? o : DefaultMonitors[metric];
        var ms = Math.Clamp(options.Interval.TotalMilliseconds, MetricCatalog.MinIntervalMs, MetricCatalog.MaxIntervalMs);
        return options with { Interval = TimeSpan.FromMilliseconds(ms), Deadband = Math.Max(1, options.Deadband) };
    }
}
