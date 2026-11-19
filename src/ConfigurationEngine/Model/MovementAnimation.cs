namespace ConfigurationEngine.Model;

using MonkeyStack.Core.Character;

/// <summary>
/// Egy mozgásállapothoz rendelt animáció. Mozgásállapotonként legfeljebb egy létezik;
/// ha hiányzik, a beépített alapértelmezett animáció érvényes.
/// </summary>
public class MovementAnimation
{
    /// <summary>Elsődleges kulcs.</summary>
    public MovementState MovementState { get; set; }

    /// <summary>A hozzárendelt <see cref="AnimationSource"/> azonosítója.</summary>
    public int AnimationSourceId { get; set; }

    /// <summary>Lejátszási sebesség, képkocka / másodperc (<see cref="MonkeyStack.Core.Animation.IAnimation.FramesPerSecond"/>).</summary>
    public double FramesPerSecond { get; set; } = 10.0;
}
