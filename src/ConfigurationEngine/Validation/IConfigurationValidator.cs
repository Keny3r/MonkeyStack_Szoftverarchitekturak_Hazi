namespace ConfigurationEngine.Validation;

using ConfigurationEngine.Model;

/// <summary>
/// Mező- és kereszthivatkozási szintű validálás (schema.md, "Validálás").
/// A médiafájlok tartalmát nem vizsgálja, az az <see cref="IMediaValidator"/> feladata.
/// Mentés előtt a konfigurációs ablak, betöltéskor a tároló hívja.
/// </summary>
public interface IConfigurationValidator
{
    /// <summary>
    /// Validálja a teljes konfigurációt. Gyors, mellékhatás nélküli; a fájlrendszert nem éri el.
    /// </summary>
    ValidationResult Validate(ConfigurationData data);
}
