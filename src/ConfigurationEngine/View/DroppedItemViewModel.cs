namespace ConfigurationEngine.View;

using ConfigurationEngine.Validation;

/// <summary>
/// Egy betöltéskor kihagyott elem a figyelmeztető sáv Részletek paneljén (view.md, "Kihagyott elemek").
/// </summary>
public sealed class DroppedItemViewModel
{
    public DroppedItemViewModel(DroppedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;
        KindText = KindName(item.Kind);
        Name = string.IsNullOrWhiteSpace(item.DisplayName) ? $"(kulcs: {item.ItemKey})" : item.DisplayName;
        Reasons = string.Join(Environment.NewLine, item.Reasons.Select(r => r.Message));
    }

    public DroppedItem Item { get; }

    /// <summary>Az elem típusa magyarul (pl. „Viselkedési szabály”).</summary>
    public string KindText { get; }

    /// <summary>Az elem neve, vagy ha nincs, a kulcsa.</summary>
    public string Name { get; }

    /// <summary>A kihagyás okai, soronként egy.</summary>
    public string Reasons { get; }

    /// <summary>Egy sorban, a mentés előtti megerősítéshez.</summary>
    public string Summary => $"{KindText}: {Name}";

    public static string KindName(ConfigurationItemKind kind) => kind switch
    {
        ConfigurationItemKind.AnimationSource => "Animációs forrás",
        ConfigurationItemKind.MovementAnimation => "Mozgási animáció",
        ConfigurationItemKind.BehaviorRule => "Viselkedési szabály",
        ConfigurationItemKind.CharacterSettings => "Karakter beállítások",
        ConfigurationItemKind.MetricPolling => "Mintavételezés",
        _ => kind.ToString()
    };
}
