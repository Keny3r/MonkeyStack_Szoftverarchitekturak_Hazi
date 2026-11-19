namespace MonkeyStack.Core.Events;

/// <summary>
/// A figyelt rendszeresemény-metrikák (README "Rendszeresemény / Metrika (OS check)").
/// Közös típus: a Konfigurációs motor szabályai és mintavételezési beállításai, valamint
/// az OS esemény értesítő motor egyaránt ezt használja. Mindegyik az <see cref="OsEventDataRegistry"/>
/// egy mezőjére képződik le.
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

    /// <summary>Megnyitott ablakok száma, db.</summary>
    WindowCount,

    /// <summary>Rendszeridő, éjfél óta eltelt percekben (0–1439).</summary>
    Time,

    /// <summary>Felhasználói inaktivitás ideje, percben.</summary>
    Inactivity
}
