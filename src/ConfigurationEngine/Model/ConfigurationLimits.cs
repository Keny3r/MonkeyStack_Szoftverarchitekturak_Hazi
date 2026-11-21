namespace ConfigurationEngine.Model;

using MonkeyStack.Core.Events;

/// <summary>
/// Zárt egész intervallum.
/// </summary>
public readonly record struct IntRange(int Min, int Max)
{
    public bool Contains(int value) => value >= Min && value <= Max;

    /// <summary>Az intervallum legközelebbi értéke.</summary>
    public int Clamp(int value) => Math.Clamp(value, Min, Max);
}

/// <summary>
/// Zárt valós intervallum.
/// </summary>
public readonly record struct DoubleRange(double Min, double Max)
{
    public bool Contains(double value) => value >= Min && value <= Max;
}

/// <summary>
/// A konfiguráció érvényes értéktartományai egy helyen, hogy a validátor és a
/// konfigurációs ablak (pl. csúszkák határai) ugyanazokat használja.
/// Forrás: docs/configuration_engine/schema.md.
/// </summary>
public static class ConfigurationLimits
{
    // Szöveges mezők
    public const int NameMaxLength = 64;
    public const int ExclusiveGroupMaxLength = 32;

    // AnimationSource
    public static readonly IntRange FramePx = new(1, 1024);
    public static readonly IntRange GridCells = new(1, 64);

    // MovementAnimation, BehaviorRule
    public static readonly DoubleRange FramesPerSecond = new(1.0, 60.0);

    // BehaviorRule
    public static readonly IntRange ZIndex = new(0, 100);
    public static readonly IntRange Priority = new(0, 100);

    // CharacterSettings
    public static readonly IntRange CharacterSizePx = new(16, 512);
    public static readonly IntRange WalkSpeedPxPerSec = new(10, 2000);
    public static readonly IntRange GravityPxPerSec2 = new(100, 10000);

    // MetricPolling
    public static readonly IntRange PollingIntervalMs = new(250, 60000);

    // Médiafájlok
    public const long MediaFileMaxBytes = 20L * 1024 * 1024;
    public const int MediaImageMaxPx = 4096;
    public const int MediaMaxFrames = 500;

    /// <summary>
    /// Egy forrás betöltött, a karakter méretére skálázott képkockáinak legnagyobb együttes mérete. Az alapméreten
    /// (100 × 100 px) mind az 500 képkocka belefér, 512 × 512 px-en 64.
    /// </summary>
    public const long MediaMaxLoadedBytes = 64L * 1024 * 1024;

    /// <summary>
    /// Egy forrás dekódolandó pixelei összesen (képkockaszám × képkockaméret), hogy egy jól tömöríthető, apró fájl
    /// ne írhasson le sok óriási képkockát. 500 darab 512 × 512 px-es képkockának elég.
    /// </summary>
    public const long MediaMaxDecodedPixels = 500L * 512 * 512;

    public static readonly IReadOnlySet<string> GifExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gif" };

    /// <summary>Sprite sheet és képsorozat-mappa esetén elfogadott kiterjesztések.</summary>
    public static readonly IReadOnlySet<string> ImageExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp" };

    /// <summary>A metrika értéktartománya, a küszöbértékek validálásához.</summary>
    public static IntRange MetricRange(Metric metric) => metric switch
    {
        Metric.Cpu or Metric.Memory or Metric.Disk or Metric.Battery => new(0, 100),
        Metric.WindowCount => new(0, 1000),
        Metric.Time => new(0, 1439),
        Metric.Inactivity => new(0, 1440),
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null)
    };
}
