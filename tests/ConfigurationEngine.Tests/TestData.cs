namespace ConfigurationEngine.Tests;

using ConfigurationEngine.Model;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

/// <summary>
/// Érvényes kiinduló konfiguráció a tesztekhez: a schema.md "Kiértékelés" példája (kis tűz, nagy tűz, pizsama).
/// </summary>
internal static class TestData
{
    public static ConfigurationData ValidConfiguration() => new()
    {
        AnimationSources =
        [
            new AnimationSource { Id = 1, Name = "Séta", SourceType = AnimationSourceType.Gif, Path = "walk.gif" },
            new AnimationSource
            {
                Id = 2,
                Name = "Tűz",
                SourceType = AnimationSourceType.SpriteSheet,
                Path = "fire.png",
                FrameWidth = 32,
                FrameHeight = 32,
                Columns = 4,
                Rows = 2,
                FrameCount = 7
            },
            new AnimationSource { Id = 3, Name = "Pizsama", SourceType = AnimationSourceType.ImageFolder, Path = "pyjama" }
        ],
        MovementAnimations =
        [
            new MovementAnimation { MovementState = MovementState.Walking, AnimationSourceId = 1, FramesPerSecond = 12 }
        ],
        BehaviorRules =
        [
            Rule(1, "Kis tűz", Metric.Cpu, ComparisonOperator.Gt, 60, sourceId: 2, group: "fire", priority: 10, zIndex: 20),
            Rule(2, "Nagy tűz", Metric.Cpu, ComparisonOperator.Gt, 80, sourceId: 2, group: "fire", priority: 20, zIndex: 20),
            Rule(3, "Pizsama", Metric.Time, ComparisonOperator.Between, 1230, upper: 360, sourceId: 3, zIndex: 10)
        ],
        Character = ConfigurationDefaults.CreateCharacterSettings(),
        MetricPollings = ConfigurationDefaults.CreateMetricPollings()
    };

    public static BehaviorRule Rule(
        int id,
        string name,
        Metric metric,
        ComparisonOperator op,
        int threshold,
        int? upper = null,
        int sourceId = 2,
        string? group = null,
        int priority = 0,
        int zIndex = 0) => new()
        {
            Id = id,
            Name = name,
            Metric = metric,
            Operator = op,
            Threshold = threshold,
            ThresholdUpper = upper,
            AnimationSourceId = sourceId,
            FramesPerSecond = 10,
            ZIndex = zIndex,
            ExclusiveGroup = group,
            Priority = priority
        };
}
