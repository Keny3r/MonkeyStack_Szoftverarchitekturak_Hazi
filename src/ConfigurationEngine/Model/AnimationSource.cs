namespace ConfigurationEngine.Model;

/// <summary>
/// Egy animációs forrás és lejátszási paraméterei.
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

    /// <summary>
    /// Egy képkocka megjelenítési ideje ezredmásodpercben.
    /// <see cref="AnimationSourceType.Gif"/> esetén opcionális; ha üres, a fájlba ágyazott időzítés érvényes.
    /// </summary>
    public int? FrameDurationMs { get; set; }

    /// <summary>Ismétlődjön-e az animáció.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Az animáció nagyítása.</summary>
    public double Scale { get; set; } = 1.0;
}
