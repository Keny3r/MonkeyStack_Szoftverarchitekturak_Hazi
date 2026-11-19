namespace ConfigurationEngine.Storage;

using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;

/// <summary>
/// A konfiguráció betöltésének eredménye.
/// </summary>
/// <param name="Data">Az érvényes elemek; a kihagyott elemeket nem tartalmazza.</param>
/// <param name="DroppedItems">A betöltéskor érvénytelennek talált, kihagyott elemek, okkal együtt.</param>
/// <param name="DatabaseWasReset">
/// Igaz, ha az adatbázis hiányzott vagy sérült volt, ezért alapértékekkel jött létre
/// (sérült fájl esetén az eredeti <c>config.db.bak</c>-ként félrekerül).
/// </param>
public sealed record ConfigurationLoadResult(
    ConfigurationData Data,
    IReadOnlyList<DroppedItem> DroppedItems,
    bool DatabaseWasReset);
