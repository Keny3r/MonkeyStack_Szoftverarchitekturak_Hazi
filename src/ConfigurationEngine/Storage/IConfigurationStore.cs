namespace ConfigurationEngine.Storage;

using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;

/// <summary>
/// A teljes konfiguráció betöltése és mentése (Wrapper Facade: a hívó nem látja az EF Core-t és az SQLite-ot).
/// </summary>
public interface IConfigurationStore
{
    /// <summary>
    /// Betölti a konfigurációt. Validál (<see cref="IConfigurationValidator"/> és
    /// <see cref="MediaValidationDepth.Quick"/>), az érvénytelen elemeket kihagyja, és
    /// a <see cref="ConfigurationLoadResult.DroppedItems"/>-ben adja vissza. Hibás adat miatt nem dob kivételt.
    /// </summary>
    Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Elmenti a teljes konfigurációt (a tárolt állapotot erre cseréli), majd kiváltja a <see cref="Changed"/> eseményt.
    /// Az új elemek (Id = 0) azonosítót kapnak.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">Ha a konfiguráció érvénytelen; ilyenkor semmi nem módosul.</exception>
    Task SaveAsync(ConfigurationData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sikeres mentés után váltódik ki. Erre iratkozik fel a konfigurációs szolgáltatás az
    /// újraindítás nélküli újratöltéshez (Component Configurator). Bármelyik szálon kiváltódhat.
    /// </summary>
    event EventHandler? Changed;
}
