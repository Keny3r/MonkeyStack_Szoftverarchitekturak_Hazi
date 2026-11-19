namespace ConfigurationEngine.Validation;

/// <summary>
/// Betöltéskor érvénytelennek talált, ezért kihagyott elem. A konfigurációs ablak
/// az okkal együtt megjeleníti, hogy a felhasználó javíthassa vagy törölhesse.
/// </summary>
/// <param name="Kind">Az elem típusa.</param>
/// <param name="ItemKey">Az elem kulcsa (lásd <see cref="ValidationError.ItemKey"/>).</param>
/// <param name="DisplayName">Megjelenített név, ha az elemnek van ilyen (pl. a szabály neve).</param>
/// <param name="Reasons">
/// A kihagyás okai. Ha az elem csak azért maradt ki, mert egy általa hivatkozott elem
/// érvénytelen (pl. a szabály animációs forrása), az is itt szerepel.
/// </param>
public sealed record DroppedItem(
    ConfigurationItemKind Kind,
    string ItemKey,
    string? DisplayName,
    IReadOnlyList<ValidationError> Reasons);
