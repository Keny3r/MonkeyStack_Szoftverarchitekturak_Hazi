namespace OsEventEngine.Pipeline;

using OsEventEngine.Monitoring;

/// <summary>
/// A feldolgozási lánc egy eleme (Interceptor minta). A lánc a Half-Sync/Half-Async szinkron rétegében,
/// egyetlen fogyasztó szálon fut, ezért az interceptorok belső állapota lock nélkül biztonságos.
/// Tervezett sorrend: Smoothing → ChangeFilter → Snapshot → Logging → kiküldés.
/// </summary>
internal interface IEventInterceptor
{
    /// <summary>
    /// Feldolgozza (és szükség esetén módosítja) a mintát.
    /// </summary>
    /// <returns>A továbbadandó minta, vagy <c>null</c>, ha a mintát el kell dobni (a lánc itt megszakad).</returns>
    MetricSample? Intercept(in MetricSample sample);
}
