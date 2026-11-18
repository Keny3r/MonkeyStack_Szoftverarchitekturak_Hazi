namespace OsEventEngine.Sources;

using OsEventEngine.Monitoring;

/// <summary>
/// Egy metrika értékének forrása (Strategy). Az élő megvalósítások Win32 Wrapper Facade-ok
/// (<c>CpuSource</c>, <c>MemorySource</c>, …), a szimulált a tesztpanel értékét adja.
/// A forrás nem tud a monitorról, a láncról és az eseményekről.
/// </summary>
internal interface IMetricSource
{
    Metric Metric { get; }

    /// <summary>
    /// Egy minta. Gyors, szinkron hívás (néhány µs – ms).
    /// </summary>
    /// <returns>
    /// Az érték a <see cref="MetricCatalog"/> szerinti egységben, vagy <c>null</c>, ha nem elérhető
    /// (pl. nincs akkumulátor, vagy a CPU első mintája, amihez még nincs előző érték).
    /// </returns>
    /// <exception cref="Exception">Hiba esetén; a monitor elkapja és <see cref="MonitorStatus.Faulted"/> állapotba lép.</exception>
    int? Sample();
}
