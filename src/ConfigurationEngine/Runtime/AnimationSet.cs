namespace ConfigurationEngine.Runtime;

using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

/// <summary>Egy animáció és a forrás, amelyből a képkockái betöltődtek (újraskálázáshoz).</summary>
internal sealed record AnimationBinding(IMutableAnimation Animation, AnimationSource Source);

/// <summary>Egy bekapcsolt szabály és az overlay animációja.</summary>
internal sealed record RuleAnimation(BehaviorRule Rule, IMutableAnimation Animation);

/// <summary>
/// A futó konfiguráció egy pillanatképe: a kész animációk és a kiértékeléshez szükséges adatok.
/// Létrehozás után nem változik (a benne lévő animációk képkockái és mérete igen), ezért a
/// <see cref="ConfigurationService"/> a záron kívül építi fel, és a zár alatt egyben cseréli le.
/// </summary>
internal sealed class AnimationSet
{
    public AnimationSet(
        IReadOnlyDictionary<MovementState, IMutableAnimation> movement,
        IReadOnlyList<RuleAnimation> rules,
        IReadOnlyList<AnimationBinding> bindings,
        IReadOnlySet<Metric> disabledMetrics,
        IReadOnlyList<MediaLoadFailure> failures)
    {
        Movement = movement;
        Rules = rules;
        Bindings = bindings;
        DisabledMetrics = disabledMetrics;
        Failures = failures;
    }

    /// <summary>A betöltés előtti állapot: nincs animáció.</summary>
    public static AnimationSet Empty { get; } = new(
        new Dictionary<MovementState, IMutableAnimation>(),
        [],
        [],
        new HashSet<Metric>(),
        []);

    /// <summary>Mozgásállapotonként egy animáció (betöltés után mind a négy állapothoz van).</summary>
    public IReadOnlyDictionary<MovementState, IMutableAnimation> Movement { get; }

    /// <summary>A bekapcsolt, betölthető forrású szabályok, a konfiguráció sorrendjében.</summary>
    public IReadOnlyList<RuleAnimation> Rules { get; }

    /// <summary>Az összes animáció a forrásával: ezeket lépteti és skálázza újra a motor.</summary>
    public IReadOnlyList<AnimationBinding> Bindings { get; }

    /// <summary>A kikapcsolt mintavételezésű metrikák; a rájuk vonatkozó szabályok nem teljesülnek.</summary>
    public IReadOnlySet<Metric> DisabledMetrics { get; }

    /// <summary>A nem betölthető források.</summary>
    public IReadOnlyList<MediaLoadFailure> Failures { get; }
}
