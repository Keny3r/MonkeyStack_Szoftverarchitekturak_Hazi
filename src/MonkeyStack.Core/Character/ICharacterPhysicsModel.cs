namespace MonkeyStack.Core.Character;

/// <summary>
/// A karakter fizikai modelljének és mozgási állapotgépének (Falling, Idle, Walking, Dragging) interfésze.
/// A karakter pozíciója (X, Y) egyetlen logikai pont: a karakter képének alsó-középső pontja (bottom-center pivot).
/// </summary>
public interface ICharacterPhysicsModel
{
    /// <summary>
    /// A karakter logikai talppontjának (alsó-középső pont) X koordinátája.
    /// </summary>
    double X { get; }

    /// <summary>
    /// A karakter logikai talppontjának (alsó-középső pont) Y koordinátája.
    /// </summary>
    double Y { get; }

    /// <summary>
    /// A karakter aktuális fizikai mozgásállapota.
    /// </summary>
    MovementState State { get; }

    /// <summary>
    /// Igaz, ha a karakter balra néz (ilyenkor a Karakter View vízszintesen tükrözi a karaktert és az overlayeket).
    /// </summary>
    bool IsFacingLeft { get; }

    /// <summary>
    /// Egyetlen fizikai tick (~60 FPS) végrehajtása az aktuális látható platformok és egér-állapot alapján.
    /// </summary>
    void Update(
        TimeSpan deltaTime,
        IReadOnlyList<PlatformSegment> platforms,
        double cursorX,
        double cursorY,
        bool isMouseDown,
        int characterWidth,
        int characterHeight);
}
