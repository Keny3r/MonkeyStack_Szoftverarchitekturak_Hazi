namespace ConfigurationEngine.Model;

using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

/// <summary>
/// A konfiguráció alapértékei (schema.md, "CharacterSettings", "MetricPolling" és "Tárolás"; a szállított overlay-ek
/// és szabályok: default_overlays.md). Ezekkel jön létre az új adatbázis, és ezek érvényesek egy hiányzó vagy hibás
/// sor helyett.
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

    /// <summary>A szállított overlay-ek forrásai (képsorozat-mappák az <c>assets/overlays</c> alatt), rögzített azonosítóval.</summary>
    public static List<AnimationSource> CreateOverlaySources() =>
    [
        Overlay(KisTuz, "Kis tűz", "kis_tuz"),
        Overlay(NagyTuz, "Nagy tűz", "nagy_tuz"),
        Overlay(Halosapka, "Hálósapka", "halosapka"),
        Overlay(Zzz, "Álmosság", "zzz"),
        Overlay(Lemerulo, "Lemerülőben", "lemerulo"),
        Overlay(Unatkozas, "Unatkozás", "unatkozas"),
        Overlay(Ablakkaosz, "Ablakkáosz", "ablakkaosz"),
        Overlay(Floppytorony, "Floppytorony", "floppytorony"),
        Overlay(Bananhejak, "Banánhéjak", "bananhejak"),
        Overlay(Cetlik, "Cetlik", "cetlik")
    ];

    /// <summary>
    /// Az alapértelmezett viselkedési szabályok (default_overlays.md). A kizáró csoportok képernyőrégiók: egy régióban
    /// egyszerre csak a legnagyobb prioritású teljesülő szabály overlay-e látszik, így több igaz szabály sem zsúfolja
    /// össze a képet. A <see cref="Cetlik"/> forráshoz nem tartozik szabály, a memóriát a <see cref="Bananhejak"/> jelzi.
    /// </summary>
    public static List<BehaviorRule> CreateDefaultRules() =>
    [
        DefaultRule(1, "Kis tűz", Metric.Cpu, ComparisonOperator.Gt, 60, KisTuz, HeadTop, 40),
        DefaultRule(2, "Nagy tűz", Metric.Cpu, ComparisonOperator.Gt, 80, NagyTuz, Sides, 20),
        DefaultRule(3, "Hálósapka", Metric.Time, ComparisonOperator.Between, 22 * 60, Halosapka, HeadTop, 10, upper: 6 * 60),
        DefaultRule(4, "Álmosság", Metric.Time, ComparisonOperator.Between, 22 * 60, Zzz, TopRight, 10, upper: 6 * 60),
        DefaultRule(5, "Lemerülőben", Metric.Battery, ComparisonOperator.Lt, 10, Lemerulo, TopLeft, 10),
        DefaultRule(6, "Unatkozás", Metric.Inactivity, ComparisonOperator.Gt, 5, Unatkozas, TopRight, 20),
        DefaultRule(7, "Ablakkáosz", Metric.WindowCount, ComparisonOperator.Gt, 10, Ablakkaosz, HeadTop, 30),
        DefaultRule(8, "Floppytorony", Metric.Disk, ComparisonOperator.Gt, 99, Floppytorony, HeadTop, 20),
        DefaultRule(9, "Banánhéjak", Metric.Memory, ComparisonOperator.Gt, 90, Bananhejak, Sides, 10)
    ];

    // A szállított overlay-források azonosítói a mozgási források (1–4) után.
    private const int KisTuz = 5, NagyTuz = 6, Halosapka = 7, Zzz = 8, Lemerulo = 9,
        Unatkozas = 10, Ablakkaosz = 11, Floppytorony = 12, Bananhejak = 13, Cetlik = 14;

    // Képernyőrégiók (kizáró csoportok) és a hozzájuk tartozó rétegsorrend.
    private const string HeadTop = "fejtető", Sides = "oldalak", TopLeft = "bal felső", TopRight = "jobb felső";

    private static int RegionZIndex(string region) => region switch
    {
        Sides => 10,
        HeadTop => 20,
        _ => 30
    };

    private static AnimationSource Overlay(int id, string name, string folder) => new()
    {
        Id = id,
        Name = name,
        SourceType = AnimationSourceType.ImageFolder,
        Path = $"overlays/{folder}"
    };

    private static BehaviorRule DefaultRule(
        int id, string name, Metric metric, ComparisonOperator op, int threshold, int sourceId, string region, int priority,
        int? upper = null) => new()
        {
            Id = id,
            Name = name,
            Metric = metric,
            Operator = op,
            Threshold = threshold,
            ThresholdUpper = upper,
            AnimationSourceId = sourceId,
            ZIndex = RegionZIndex(region),
            ExclusiveGroup = region,
            Priority = priority
        };

    private static int MovementSourceId(MovementState state) => state switch
    {
        MovementState.Idle => 1,
        MovementState.Walking => 2,
        MovementState.Falling => 3,
        MovementState.Dragging => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}
