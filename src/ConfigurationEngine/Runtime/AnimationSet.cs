namespace ConfigurationEngine.Runtime;

using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

/// <summary>Egy bekapcsolt szabály és az overlay animációja.</summary>
internal sealed record RuleAnimation(BehaviorRule Rule, IMutableAnimation Animation);

/// <summary>
/// A futó konfiguráció egy pillanatképe: a kész animációk és a kiértékeléshez szükséges adatok.
/// Létrehozás után nem változik (a benne lévő animációk mérete, képkockái és léptetése igen), ezért a
/// <see cref="ConfigurationService"/> a záron kívül építi fel, és a zár alatt egyben cseréli le.
/// </summary>
internal sealed class AnimationSet
{
    public AnimationSet(
        ConfigurationData? data,
        IReadOnlyDictionary<MovementState, IMutableAnimation> movement,
        IReadOnlyList<RuleAnimation> rules,
        IReadOnlySet<Metric> disabledMetrics,
        IReadOnlyList<MediaLoadFailure> failures,
        IReadOnlyDictionary<IMutableAnimation, AnimationSource> sources)
    {
        Data = data;
        Movement = movement;
        Rules = rules;
        Animations = movement.Values.Concat(rules.Select(r => r.Animation)).ToArray();
        DisabledMetrics = disabledMetrics;
        Failures = failures;
        Sources = sources;
    }

    /// <summary>A betöltés előtti állapot: nincs animáció.</summary>
    public static AnimationSet Empty { get; } = new(
        null,
        new Dictionary<MovementState, IMutableAnimation>(),
        [],
        new HashSet<Metric>(),
        [],
        new Dictionary<IMutableAnimation, AnimationSource>());

    /// <summary>
    /// A konfiguráció, amelyből a pillanatkép készült; a betöltés előtt <c>null</c>.
    /// Csak a motor látja, és nem módosítja.
    /// </summary>
    public ConfigurationData? Data { get; }

    /// <summary>Mozgásállapotonként egy animáció (betöltés után mind a négy állapothoz van).</summary>
    public IReadOnlyDictionary<MovementState, IMutableAnimation> Movement { get; }

    /// <summary>A bekapcsolt, betölthető forrású szabályok, a konfiguráció sorrendjében.</summary>
    public IReadOnlyList<RuleAnimation> Rules { get; }

    /// <summary>Az összes animáció: ezeket lépteti és méretezi át a motor.</summary>
    public IReadOnlyList<IMutableAnimation> Animations { get; }

    /// <summary>A kikapcsolt mintavételezésű metrikák; a rájuk vonatkozó szabályok nem teljesülnek.</summary>
    public IReadOnlySet<Metric> DisabledMetrics { get; }

    /// <summary>A felépítéskor nem betölthető források.</summary>
    public IReadOnlyList<MediaLoadFailure> Failures { get; }

    /// <summary>
    /// Animációnként a forrás, amelynek képkockáit mutatja (a szállított alapanimációnál annak forrása); méretváltáskor
    /// ezekből töltődnek be az új méretű képkockák. Az üres animáció nem szerepel benne.
    /// </summary>
    public IReadOnlyDictionary<IMutableAnimation, AnimationSource> Sources { get; }
}
