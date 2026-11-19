namespace ConfigurationEngine.Storage;

using ConfigurationEngine.Model;

/// <summary>
/// Másolatok és normalizálás mentéshez: az EF Core a másolatokat követi, így a hívó objektumai
/// csak sikeres mentés után változnak (új azonosítók).
/// </summary>
internal static class ModelCopies
{
    public static AnimationSource Copy(this AnimationSource s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        SourceType = s.SourceType,
        Path = s.Path,
        FrameWidth = s.FrameWidth,
        FrameHeight = s.FrameHeight,
        Columns = s.Columns,
        Rows = s.Rows,
        FrameCount = s.FrameCount
    };

    public static MovementAnimation Copy(this MovementAnimation m) => new()
    {
        MovementState = m.MovementState,
        AnimationSourceId = m.AnimationSourceId,
        FramesPerSecond = m.FramesPerSecond
    };

    public static BehaviorRule Copy(this BehaviorRule r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        IsEnabled = r.IsEnabled,
        Metric = r.Metric,
        Operator = r.Operator,
        Threshold = r.Threshold,
        ThresholdUpper = r.ThresholdUpper,
        AnimationSourceId = r.AnimationSourceId,
        FramesPerSecond = r.FramesPerSecond,
        ZIndex = r.ZIndex,
        ExclusiveGroup = r.ExclusiveGroup,
        Priority = r.Priority
    };

    public static CharacterSettings Copy(this CharacterSettings c) => new()
    {
        Id = c.Id,
        CharacterWidth = c.CharacterWidth,
        CharacterHeight = c.CharacterHeight,
        WalkSpeedPxPerSec = c.WalkSpeedPxPerSec,
        GravityPxPerSec2 = c.GravityPxPerSec2,
        FollowCursor = c.FollowCursor
    };

    public static MetricPolling Copy(this MetricPolling p) => new()
    {
        Metric = p.Metric,
        IsEnabled = p.IsEnabled,
        IntervalMs = p.IntervalMs
    };

    /// <summary>
    /// A szöveges mezők trimmelése (schema.md: "trimmelve tároljuk"); az üres kizáró csoport <c>null</c> lesz.
    /// A hívó objektumain dolgozik, mentés előtt.
    /// </summary>
    public static void Normalize(ConfigurationData data)
    {
        foreach (var source in data.AnimationSources)
        {
            source.Name = source.Name?.Trim() ?? string.Empty;
            source.Path = source.Path?.Trim() ?? string.Empty;
        }

        foreach (var rule in data.BehaviorRules)
        {
            rule.Name = rule.Name?.Trim() ?? string.Empty;
            rule.ExclusiveGroup = string.IsNullOrWhiteSpace(rule.ExclusiveGroup) ? null : rule.ExclusiveGroup.Trim();
        }
    }
}
