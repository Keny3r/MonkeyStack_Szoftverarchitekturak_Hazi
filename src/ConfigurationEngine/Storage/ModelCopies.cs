namespace ConfigurationEngine.Storage;

using ConfigurationEngine.Model;

/// <summary>
/// Másolatok és normalizálás mentéshez: a tároló a hívó konfigurációjának másolatát normalizálja, validálja és menti
/// (az EF Core is másolatokat követ), így a hívó objektumai csak sikeres mentés után változnak
/// (<see cref="ApplySaved"/>: új azonosítók, átírt hivatkozások, trimmelt szövegek).
/// </summary>
/// <remarks>
/// Új modellmezőnél a megfelelő <c>Copy</c> metódust is bővíteni kell; ezt a <c>ModelCopiesTests</c> ellenőrzi.
/// </remarks>
internal static class ModelCopies
{
    /// <summary>A teljes konfiguráció mély másolata; az elemek sorrendje megegyezik.</summary>
    public static ConfigurationData Copy(this ConfigurationData d) => new()
    {
        AnimationSources = d.AnimationSources.Select(s => s.Copy()).ToList(),
        MovementAnimations = d.MovementAnimations.Select(m => m.Copy()).ToList(),
        BehaviorRules = d.BehaviorRules.Select(r => r.Copy()).ToList(),

        // A hiányzó beállításokat a validátor jelzi, ezért a null itt megmarad.
        Character = d.Character?.Copy()!,
        MetricPollings = d.MetricPollings.Select(p => p.Copy()).ToList()
    };

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
    /// A mentendő másolaton dolgozik, validálás előtt.
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

    /// <summary>
    /// Sikeres mentés után a mentett másolatban megváltozott mezőket (új azonosítók, átírt forráshivatkozások,
    /// normalizált szövegek) visszaírja a hívó objektumaiba. A <paramref name="saved"/> a <paramref name="target"/>
    /// másolata, így az elemek indexre megfelelnek egymásnak.
    /// </summary>
    public static void ApplySaved(ConfigurationData saved, ConfigurationData target)
    {
        for (var i = 0; i < saved.AnimationSources.Count; i++)
        {
            var (from, to) = (saved.AnimationSources[i], target.AnimationSources[i]);
            to.Id = from.Id;
            to.Name = from.Name;
            to.Path = from.Path;
        }

        for (var i = 0; i < saved.MovementAnimations.Count; i++)
        {
            target.MovementAnimations[i].AnimationSourceId = saved.MovementAnimations[i].AnimationSourceId;
        }

        for (var i = 0; i < saved.BehaviorRules.Count; i++)
        {
            var (from, to) = (saved.BehaviorRules[i], target.BehaviorRules[i]);
            to.Id = from.Id;
            to.Name = from.Name;
            to.ExclusiveGroup = from.ExclusiveGroup;
            to.AnimationSourceId = from.AnimationSourceId;
        }
    }
}
