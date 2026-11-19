namespace ConfigurationEngine.Validation;

/// <summary>
/// Egy validálási hiba.
/// </summary>
/// <param name="Kind">Az érintett elem típusa.</param>
/// <param name="ItemKey">
/// Az elem kulcsa: <c>Id</c> (<see cref="ConfigurationItemKind.AnimationSource"/>, <see cref="ConfigurationItemKind.BehaviorRule"/>),
/// a <c>MovementState</c> vagy a <c>Metric</c> neve, illetve <c>"1"</c> a <see cref="ConfigurationItemKind.CharacterSettings"/> esetén.
/// Negatív ideiglenes azonosítójú új elemnél is az <c>Id</c>; ha az új elem <c>Id</c>-ja 0, a listabeli index <c>"#index"</c> alakban.
/// </param>
/// <param name="PropertyName">Az érintett mező neve (pl. <c>nameof(BehaviorRule.Threshold)</c>); <c>null</c>, ha az elem egészére vonatkozik.</param>
/// <param name="Message">A felhasználónak megjeleníthető, magyar nyelvű hibaüzenet.</param>
public sealed record ValidationError(
    ConfigurationItemKind Kind,
    string ItemKey,
    string? PropertyName,
    string Message);
