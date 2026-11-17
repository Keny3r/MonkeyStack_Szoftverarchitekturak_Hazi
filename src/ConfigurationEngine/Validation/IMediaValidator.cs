namespace ConfigurationEngine.Validation;

using ConfigurationEngine.Model;

/// <summary>
/// A médiafájlok ellenőrzésének mélysége.
/// </summary>
public enum MediaValidationDepth
{
    /// <summary>
    /// Útvonal (nincs hálózati útvonal), létezés, kiterjesztés és fájlméret. Betöltéskor fut.
    /// </summary>
    Quick,

    /// <summary>
    /// A <see cref="Quick"/> ellenőrzések, valamint a kép tényleges dekódolása, a képméret,
    /// a képkockaszám, a sprite sheet rácsa és a képsorozat képeinek azonos mérete. Mentéskor fut.
    /// </summary>
    Full
}

/// <summary>
/// Az animációs források fájljainak ellenőrzése (schema.md, "Médiafájlok validálása").
/// A tároló betöltéskor <see cref="MediaValidationDepth.Quick"/>, a konfigurációs ablak mentés előtt
/// <see cref="MediaValidationDepth.Full"/> mélységben hívja.
/// </summary>
public interface IMediaValidator
{
    /// <summary>
    /// Ellenőrzi egy animációs forrás fájlját vagy mappáját. A hibák kulcsa a forrás <c>Id</c>-ja.
    /// </summary>
    Task<IReadOnlyList<ValidationError>> ValidateAsync(
        AnimationSource source,
        MediaValidationDepth depth,
        CancellationToken cancellationToken = default);
}
