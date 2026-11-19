namespace ConfigurationEngine.Model;

/// <summary>
/// A karakter globális beállításai. Egyetlen példány létezik (<see cref="Id"/> = 1);
/// az alapértékek a schema.md "CharacterSettings" táblájának felelnek meg.
/// </summary>
public class CharacterSettings
{
    /// <summary>Mindig 1.</summary>
    public int Id { get; set; } = 1;

    /// <summary>
    /// Az animációk egységes szélessége pixelben
    /// (<see cref="MonkeyStack.Core.Configuration.IConfigurationService.CharacterWidth"/>).
    /// </summary>
    public int CharacterWidth { get; set; } = 100;

    /// <summary>
    /// Az animációk egységes magassága pixelben
    /// (<see cref="MonkeyStack.Core.Configuration.IConfigurationService.CharacterHeight"/>).
    /// </summary>
    public int CharacterHeight { get; set; } = 100;

    /// <summary>Séta sebessége, pixel / másodperc.</summary>
    public int WalkSpeedPxPerSec { get; set; } = 120;

    /// <summary>Gravitáció, pixel / másodperc².</summary>
    public int GravityPxPerSec2 { get; set; } = 2000;

    /// <summary>Kövesse-e a karakter az egérkurzort.</summary>
    public bool FollowCursor { get; set; } = true;
}
