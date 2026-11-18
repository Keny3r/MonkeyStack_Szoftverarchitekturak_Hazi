namespace OsEventEngine.Monitoring;

/// <summary>
/// A figyelt rendszeresemény-metrikák (README "Rendszeresemény / Metrika (OS check)").
/// Értékei és sorrendje megegyezik a konfigurációs ág <c>ConfigurationEngine.Model.Metric</c> felsorolásával.
/// Átmenetileg itt van; a cél egy közös <c>MonkeyStack.Core.Events.Metric</c> (docs/os_event_engine/plan.md, 10. fejezet).
/// </summary>
public enum Metric
{
    /// <summary>Processzorterhelés, %.</summary>
    Cpu,

    /// <summary>Memóriahasználat, %.</summary>
    Memory,

    /// <summary>Háttértár-telítettség, %.</summary>
    Disk,

    /// <summary>Akkumulátortöltöttség, %.</summary>
    Battery,

    /// <summary>Megnyitott ("Alt-Tab") ablakok száma, db. A saját folyamat ablakai nem számítanak.</summary>
    WindowCount,

    /// <summary>Rendszeridő, éjfél óta eltelt percekben (0–1439).</summary>
    Time,

    /// <summary>Felhasználói inaktivitás ideje, percben.</summary>
    Inactivity
}
