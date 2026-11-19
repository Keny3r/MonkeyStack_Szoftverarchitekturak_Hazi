namespace ConfigurationEngine.Model;

using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

/// <summary>
/// A konfiguráció alapértékei (schema.md, "CharacterSettings", "MetricPolling" és "Tárolás").
/// Ezekkel jön létre az új adatbázis, és ezek érvényesek egy hiányzó vagy hibás sor helyett.
/// </summary>
public static class ConfigurationDefaults
{
    /// <summary>Az alapértelmezett karakter beállítások (új példány).</summary>
    public static CharacterSettings CreateCharacterSettings() => new();

    /// <summary>A metrika alapértelmezett lekérdezési gyakorisága ezredmásodpercben.</summary>
    public static int PollingIntervalMs(Metric metric) => metric switch
    {
        Metric.Cpu or Metric.Memory => 1_000,
        Metric.Inactivity or Metric.WindowCount => 2_000,
        Metric.Disk or Metric.Battery or Metric.Time => 30_000,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null)
    };

    /// <summary>Az alapértelmezett mintavételezési beállítás egy metrikához (új példány).</summary>
    public static MetricPolling CreateMetricPolling(Metric metric) => new()
    {
        Metric = metric,
        IsEnabled = true,
        IntervalMs = PollingIntervalMs(metric)
    };

    /// <summary>Az összes metrika alapértelmezett mintavételezési beállítása, a felsorolás sorrendjében.</summary>
    public static List<MetricPolling> CreateMetricPollings() =>
        Enum.GetValues<Metric>().Select(CreateMetricPolling).ToList();

    /// <summary>
    /// A mozgásállapot szállított alapanimációjának forrása (schema.md, "Tárolás"): képsorozat-mappa az alkalmazás
    /// <c>assets</c> mappájában. Az azonosítót az első migráció rögzíti.
    /// </summary>
    public static AnimationSource CreateMovementSource(MovementState state) => new()
    {
        Id = MovementSourceId(state),
        Name = $"Alap – {state}",
        SourceType = AnimationSourceType.ImageFolder,
        Path = $"movement/{state.ToString().ToLowerInvariant()}"
    };

    /// <summary>A mozgásállapot alapértelmezett mozgási animációja a megadott forrással.</summary>
    public static MovementAnimation CreateMovementAnimation(MovementState state, int animationSourceId) => new()
    {
        MovementState = state,
        AnimationSourceId = animationSourceId
    };

    /// <summary>Az összes mozgásállapot szállított alapanimációjának forrása, a felsorolás sorrendjében.</summary>
    public static List<AnimationSource> CreateMovementSources() =>
        Enum.GetValues<MovementState>().Select(CreateMovementSource).ToList();

    /// <summary>Az összes mozgásállapot alapértelmezett mozgási animációja a szállított forrásokkal.</summary>
    public static List<MovementAnimation> CreateMovementAnimations() =>
        Enum.GetValues<MovementState>().Select(s => CreateMovementAnimation(s, MovementSourceId(s))).ToList();

    private static int MovementSourceId(MovementState state) => state switch
    {
        MovementState.Idle => 1,
        MovementState.Walking => 2,
        MovementState.Falling => 3,
        MovementState.Dragging => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}
