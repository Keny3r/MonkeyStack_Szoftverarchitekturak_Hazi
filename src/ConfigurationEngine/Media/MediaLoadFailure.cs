namespace ConfigurationEngine.Media;

/// <summary>
/// Egy animációs forrás, amely a futó konfiguráció felépítésekor nem töltődött be (pl. a fájlt mentés óta törölték).
/// A rá hivatkozó mozgásállapot ilyenkor a szállított alapanimációt kapja, a rá hivatkozó szabályok nem jelennek meg.
/// </summary>
/// <param name="AnimationSourceId">A forrás azonosítója.</param>
/// <param name="SourceName">A forrás megjelenített neve.</param>
/// <param name="Message">A hiba magyar nyelvű leírása (<see cref="MediaLoadException"/>).</param>
/// <param name="PropertyName">A hibás mező (<see cref="MediaLoadException.PropertyName"/>), ha a hiba mezőhöz kötődik.</param>
public sealed record MediaLoadFailure(int AnimationSourceId, string SourceName, string Message, string? PropertyName);
