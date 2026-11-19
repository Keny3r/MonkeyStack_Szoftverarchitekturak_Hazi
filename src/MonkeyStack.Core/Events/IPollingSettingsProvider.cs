namespace MonkeyStack.Core.Events;

/// <summary>
/// A rendszeresemény-metrikák mintavételezési beállításai az OS esemény értesítő motor számára.
/// A Konfigurációs motor valósítja meg (az <see cref="Configuration.IConfigurationService"/> mellett,
/// Extension Interface minta), a host adja át az OS motornak, így az OS motor csak a Core-tól függ.
/// </summary>
public interface IPollingSettingsProvider
{
    /// <summary>
    /// A metrika lekérdezési gyakorisága. <c>null</c>: a metrika ki van kapcsolva, nem kell mérni,
    /// és nem kell rá eseményt küldeni.
    /// </summary>
    TimeSpan? GetPollingInterval(Metric metric);

    /// <summary>
    /// Akkor tüzel, ha legalább egy metrika gyakorisága vagy be-/kikapcsolt állapota megváltozott
    /// (más konfigurációs változásra nem). Bármelyik szálon kiváltódhat.
    /// </summary>
    event EventHandler? PollingSettingsChanged;
}
