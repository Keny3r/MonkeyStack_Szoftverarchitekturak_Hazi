namespace CharacterEngine;

using MonkeyStack.Core.Character;

/// <summary>
/// A karakter fizikai modelljének és mozgási állapotgépének váza.
/// A karakter pozíciója (X, Y) a karakter képének alsó-középső pontja (bottom-center pivot).
/// Állapotmentesen kapja meg a látható ablakperemeket (PlatformSegment) minden tickben (~60 FPS).
/// </summary>
public class CharacterPhysicsModel : ICharacterPhysicsModel
{
    /// <summary>
    /// Sétáló módban a platform szélétől tartott távolság pixelben, ahol a karakter megfordul.
    /// </summary>
    public const double PlatformEdgeMargin = 10.0;

    /// <summary>
    /// Landolás után ennyi ideig marad Idle állapotban a karakter, mielőtt elindulna sétálni.
    /// </summary>
    public static readonly TimeSpan IdleDuration = TimeSpan.FromSeconds(2.0);

    public double X { get; set; }
    public double Y { get; set; }
    public MovementState State { get; private set; } = MovementState.Falling;
    public bool IsFacingLeft { get; private set; }

    public void Update(
        TimeSpan deltaTime,
        IReadOnlyList<PlatformSegment> platforms,
        double cursorX,
        double cursorY,
        bool isMouseDown,
        int characterWidth,
        int characterHeight)
    {
        // TODO (Karakter / Fizika csapat):
        // 1. Ha isMouseDown igaz és a kurzor a karakter befoglaló téglalapja [X - W/2 .. X + W/2, Y - H .. Y] fölött van -> State = Dragging.
        // 2. Dragging: követi a kurzort; ha !isMouseDown -> State = Falling.
        // 3. Falling: esik lefelé; ha van alatta platform -> ráilleszkedik (Y = platform.Y) és State = Idle (2 mp-ig).
        // 4. Idle: folyamatosan ellenőrzi, van-e még alatta platform (ha nincs -> Falling); 2 mp után -> State = Walking random iránnyal.
        // 5. Walking: folyamatosan ellenőrzi, van-e még alatta platform (ha nincs -> Falling);
        //    a platform széléig (PlatformEdgeMargin = 10px ráhagyással) sétál, majd megfordul (IsFacingLeft = !IsFacingLeft).
    }
}
