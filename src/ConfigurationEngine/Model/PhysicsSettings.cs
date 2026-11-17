namespace ConfigurationEngine.Model;

/// <summary>
/// A karakter mozgási és fizikai paraméterei. Egyetlen példány létezik (<see cref="Id"/> = 1);
/// az alapértékek a schema.md "PhysicsSettings" táblájának felelnek meg.
/// </summary>
public class PhysicsSettings
{
    /// <summary>Mindig 1.</summary>
    public int Id { get; set; } = 1;

    public int WalkSpeedPxPerSec { get; set; } = 120;

    public int GravityPxPerSec2 { get; set; } = 2000;

    /// <summary>A karakter nagyítása.</summary>
    public double CharacterScale { get; set; } = 1.0;

    /// <summary>Kövesse-e a karakter az egérkurzort.</summary>
    public bool FollowCursor { get; set; } = true;
}
