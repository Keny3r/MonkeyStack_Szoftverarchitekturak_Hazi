namespace ConfigurationEngine.Model;

/// <summary>
/// A szabályokban használható rendszeresemény-metrikák.
/// Mindegyik az <see cref="MonkeyStack.Core.Events.OsEventDataRegistry"/> egy mezőjére képződik le,
/// az értéktartományokat a <see cref="ConfigurationLimits.MetricRange"/> adja meg.
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
