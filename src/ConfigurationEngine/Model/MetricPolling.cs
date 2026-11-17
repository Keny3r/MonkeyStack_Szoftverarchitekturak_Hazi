namespace ConfigurationEngine.Model;

/// <summary>
/// Egy rendszeresemény-metrika mintavételezési beállítása. Metrikánként egy példány.
/// </summary>
public class MetricPolling
{
    /// <summary>Elsődleges kulcs.</summary>
    public Metric Metric { get; set; }

    /// <summary>Kikapcsolt metrikát nem kérdezünk le, így a rá vonatkozó szabályok sem teljesülnek.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Lekérdezési gyakoriság ezredmásodpercben.</summary>
    public int IntervalMs { get; set; }
}
