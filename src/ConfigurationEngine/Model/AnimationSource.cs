namespace ConfigurationEngine.Model;

/// <summary>
/// Egy animációs forrás: honnan és hogyan kell betölteni a képkockákat.
/// A lejátszási sebesség és a rétegsorrend nem itt, hanem a felhasználás helyén
/// (<see cref="MovementAnimation"/>, <see cref="BehaviorRule"/>) van, így ugyanaz a forrás
/// több helyen, eltérő beállítással is használható.
/// Leírás és érvényes értékek: docs/configuration_engine/schema.md, "AnimationSource".
/// </summary>
public class AnimationSource
{
    /// <summary>Elsődleges kulcs; új, még nem mentett forrásnál 0.</summary>
    public int Id { get; set; }

    /// <summary>Egyedi, megjelenített név.</summary>
    public string Name { get; set; } = string.Empty;

    public AnimationSourceType SourceType { get; set; }

    /// <summary>
    /// Fájl (<see cref="AnimationSourceType.Gif"/>, <see cref="AnimationSourceType.SpriteSheet"/>)
    /// vagy mappa (<see cref="AnimationSourceType.ImageFolder"/>) útvonala.
    /// A relatív útvonal az alkalmazás <c>assets</c> mappájához képest értelmeződik.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Egy képkocka szélessége pixelben. Csak <see cref="AnimationSourceType.SpriteSheet"/> esetén.</summary>
    public int? FrameWidth { get; set; }

    /// <summary>Egy képkocka magassága pixelben. Csak <see cref="AnimationSourceType.SpriteSheet"/> esetén.</summary>
    public int? FrameHeight { get; set; }

    /// <summary>A rács oszlopainak száma. Csak <see cref="AnimationSourceType.SpriteSheet"/> esetén.</summary>
    public int? Columns { get; set; }

    /// <summary>A rács sorainak száma. Csak <see cref="AnimationSourceType.SpriteSheet"/> esetén.</summary>
    public int? Rows { get; set; }

    /// <summary>A képkockák száma (az utolsó sor lehet hiányos). Csak <see cref="AnimationSourceType.SpriteSheet"/> esetén.</summary>
    public int? FrameCount { get; set; }
}
