namespace ConfigurationEngine.Storage;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Felsorolás ↔ szöveg átalakítás az adatbázishoz. Az ismeretlen szöveget (pl. kézzel szerkesztett
/// adatbázisban <c>Metric = "Gpu"</c>) nem kivétellel jelzi, hanem egy nem definiált értékre (-1) képezi le,
/// amit a validátor kiszűr, így csak az érintett sor marad ki betöltéskor (schema.md, "Validálás").
/// </summary>
internal sealed class TolerantEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public TolerantEnumConverter()
        : base(value => value.ToString(), text => Parse(text))
    {
    }

    private static TEnum Parse(string text) =>
        Enum.TryParse<TEnum>(text, ignoreCase: false, out var value) && Enum.IsDefined(value)
            ? value
            : (TEnum)Enum.ToObject(typeof(TEnum), -1);
}
