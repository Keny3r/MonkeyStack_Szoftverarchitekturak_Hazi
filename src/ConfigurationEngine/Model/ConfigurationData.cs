namespace ConfigurationEngine.Model;

/// <summary>
/// A teljes, szerkeszthető konfiguráció. Ezt tölti be és menti az
/// <see cref="Storage.IConfigurationStore"/>, és ezt szerkeszti a konfigurációs ablak.
/// A hivatkozások azonosítóval történnek (pl. <see cref="BehaviorRule.AnimationSourceId"/>).
/// </summary>
public class ConfigurationData
{
    public List<AnimationSource> AnimationSources { get; set; } = [];

    public List<MovementAnimation> MovementAnimations { get; set; } = [];

    public List<BehaviorRule> BehaviorRules { get; set; } = [];

    public CharacterSettings Character { get; set; } = new();

    public List<MetricPolling> MetricPollings { get; set; } = [];
}
