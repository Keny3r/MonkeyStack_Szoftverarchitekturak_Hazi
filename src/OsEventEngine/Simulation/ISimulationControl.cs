namespace OsEventEngine.Simulation;

using MonkeyStack.Core.Events;

/// <summary>
/// Egy metrika forrásának módja.
/// </summary>
public enum SourceMode
{
    /// <summary>Valódi Win32 mérés.</summary>
    Live,

    /// <summary>A beállított szimulált érték; a valódi mérés ilyenkor le sem fut.</summary>
    Simulated
}

/// <summary>
/// A szimuláció vezérlése (OS Mock View csúszkái, illetve más komponensek tesztjei).
/// A szimulált érték ugyanazon a láncon megy át, mint az élő (docs/os_event_engine/plan.md, ADR-5),
/// így 100%-os CPU a gép valódi terhelése nélkül tesztelhető.
/// </summary>
public interface ISimulationControl
{
    /// <summary>A szimulálható metrikák leírói (csúszka-tartományok, megjelenített név).</summary>
    IReadOnlyList<MetricDescriptor> Metrics { get; }

    SourceMode GetMode(Metric metric);

    /// <summary>
    /// Módváltás. Szimulált módba váltáskor a szimulált érték az utolsó élő érték lesz (nincs ugrás).
    /// Utána azonnali mintavétel indul, és az első minta a változásszűrőn mindig átjut.
    /// </summary>
    void SetMode(Metric metric, SourceMode mode);

    /// <summary>
    /// Beállítja a szimulált értéket, és a metrikát szimulált módba teszi. Az esemény &lt; 100 ms-on belül megy ki.
    /// Az érték a <see cref="MetricDescriptor.Min"/>–<see cref="MetricDescriptor.Max"/> tartományra vágódik.
    /// </summary>
    void SetSimulatedValue(Metric metric, int value);

    /// <summary>Több metrika egyszerre, mind szimulált módba (pl. "Túlmelegedés": CPU 95, Memória 92).</summary>
    void ApplyPreset(IReadOnlyDictionary<Metric, int> values);

    /// <summary>Minden metrika vissza élő módba.</summary>
    void ResetAll();
}
