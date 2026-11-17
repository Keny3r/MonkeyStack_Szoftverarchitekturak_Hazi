namespace ConfigurationEngine.Model;

/// <summary>
/// Viselkedési szabály: egy triggerfeltétel és a hozzá rendelt overlay animáció.
/// Kiértékelés és érvényes értékek: docs/configuration_engine/schema.md, "BehaviorRule".
/// </summary>
public class BehaviorRule
{
    /// <summary>Elsődleges kulcs; új, még nem mentett szabálynál 0.</summary>
    public int Id { get; set; }

    /// <summary>Megjelenített név.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Kikapcsolt szabály tárolódik, de nem értékelődik ki.</summary>
    public bool IsEnabled { get; set; } = true;

    public Metric Metric { get; set; }

    public ComparisonOperator Operator { get; set; }

    /// <summary>Küszöbérték; <see cref="ComparisonOperator.Between"/> esetén az alsó határ.</summary>
    public int Threshold { get; set; }

    /// <summary>Felső határ; csak <see cref="ComparisonOperator.Between"/> esetén van értéke.</summary>
    public int? ThresholdUpper { get; set; }

    /// <summary>A teljesüléskor megjelenő overlay <see cref="AnimationSource"/> azonosítója.</summary>
    public int AnimationSourceId { get; set; }

    /// <summary>Kirajzolási sorrend: a nagyobb érték kerül felülre.</summary>
    public int ZOrder { get; set; }

    /// <summary>
    /// Kizáró csoport neve. Azonos csoportból egyszerre csak a legnagyobb <see cref="Priority"/>-jú
    /// teljesülő szabály aktív. <c>null</c>: nincs csoport.
    /// </summary>
    public string? ExclusiveGroup { get; set; }

    /// <summary>Csoporton belüli prioritás; a nagyobb nyer, egyenlőségnél a kisebb <see cref="Id"/>.</summary>
    public int Priority { get; set; }
}
