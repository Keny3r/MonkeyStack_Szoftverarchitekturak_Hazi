namespace OsEventEngine.Monitoring;

using OsEventEngine.Simulation;

/// <summary>
/// Egy monitor életciklus-állapota (az OS Mock View "Monitor" oszlopa).
/// </summary>
public enum MonitorStatus
{
    /// <summary>Még nem indult el, vagy le lett állítva (<see cref="IConfigurableComponent.Fini"/>).</summary>
    Stopped,

    /// <summary>A konfigurált periódussal mintavételez.</summary>
    Running,

    /// <summary>Le van tiltva (<see cref="MonitorOptions.Enabled"/> = false), nem mér és nem küld eseményt.</summary>
    Suspended,

    /// <summary>A forrás kivételt dobott; exponenciális visszalépéssel (max. 60 s) újrapróbálja.</summary>
    Faulted
}

/// <summary>
/// Egy metrika mintavételező monitora (Half-Sync/Half-Async: az aszinkron réteg eleme).
/// Extension Interface: ugyanaz az objektum megvalósítja a <see cref="IConfigurableComponent"/>-et
/// és (ha szimulálható) az <see cref="ISimulatable"/>-t is; a kliensek csak a nekik szóló nézetet kérik el.
/// </summary>
internal interface IMetricMonitor
{
    Metric Metric { get; }

    /// <summary>
    /// Azonnali mintavételt kér, a periódus kivárása nélkül (Reactor értesítés, csúszka mozgatása).
    /// Nem blokkol: csak jelet ad a monitor saját ciklusának (Active Object jelleg).
    /// </summary>
    void TriggerNow();
}

/// <summary>
/// Component Configurator: futás közben, újraindítás nélkül (újra)konfigurálható komponens.
/// </summary>
internal interface IConfigurableComponent
{
    /// <summary>(Újra)indítás a megadott beállítással. Intervallumváltáskor is ezt hívjuk.</summary>
    void Init(MonitorOptions options);

    /// <summary>Szünetelteti a mintavételt (pl. a metrika letiltásakor).</summary>
    void Suspend();

    /// <summary>Folytatja a mintavételt.</summary>
    void Resume();

    /// <summary>Végleges leállítás, erőforrások felszabadítása.</summary>
    void Fini();

    MonitorStatus Status { get; }

    /// <summary>Ember által olvasható állapot, pl. <c>"Running @ 1000 ms"</c>.</summary>
    string Info();
}

/// <summary>
/// Szimulálható monitor (Extension Interface). Ami nem valósítja meg, azt a tesztpanel nem tudja felülírni.
/// </summary>
internal interface ISimulatable
{
    SourceMode Mode { get; set; }

    /// <summary>A szimulált érték; csak <see cref="SourceMode.Simulated"/> módban használjuk.</summary>
    int SimulatedValue { get; set; }
}
