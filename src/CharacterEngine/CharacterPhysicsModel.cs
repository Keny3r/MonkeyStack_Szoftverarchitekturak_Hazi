namespace CharacterEngine;

using CharacterEngine.Physics;
using CharacterEngine.StateMachine;
using MonkeyStack.Core.Character;

/// <summary>
/// A karakter fizikai modellje: az <see cref="ICharacterPhysicsModel"/> homlokzata a belső fizikai motor
/// (<see cref="IPhysicsEngine"/>) és a mozgási állapotgép (<see cref="IMovementStateMachine"/>) előtt
/// (docs/character_engine/architecture.md, 2. pont). Saját logikája nincs, a hívásokat az állapotgépnek adja tovább.
/// A karakter pozíciója (X, Y) a karakter képének alsó-középső pontja (bottom-center pivot).
/// Állapotmentesen kapja meg a látható ablakperemeket (PlatformSegment) minden tickben (~60 FPS).
/// </summary>
/// <remarks>
/// Nem szálbiztos. Feltevés, amelyet még egyeztetni kell a Karakter motor másik felelősével: az <see cref="Update"/>-et és a
/// tulajdonságokat kizárólag a <c>RenderManager</c> hívja a saját zárja alatt, a <see cref="SetPosition"/>-t pedig csak a
/// léptető (<c>IFrameLoop</c>) bekötése előtt, vagy ugyanezen zár alatt szabad hívni.
/// </remarks>
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

    private readonly IMovementStateMachine _stateMachine;

    /// <summary>
    /// A futó alkalmazás modellje: valódi fizikai motor az alapértelmezett paraméterekkel
    /// (<see cref="PhysicsSettings.Default"/>) és véletlen kezdőirányú séta.
    /// </summary>
    public CharacterPhysicsModel()
        : this(new MovementStateMachine(new PhysicsEngine()))
    {
    }

    /// <summary>Saját állapotgéppel (pl. tesztekben, rögzített paraméterekkel vagy iránnyal).</summary>
    public CharacterPhysicsModel(IMovementStateMachine stateMachine)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
    }

    public double X => _stateMachine.X;

    public double Y => _stateMachine.Y;

    public MovementState State => _stateMachine.State;

    public bool IsFacingLeft => _stateMachine.IsFacingLeft;

    /// <summary>
    /// A karakter áthelyezése (pl. a kezdőpozíció beállítása indításkor). A karakter innen esésbe kezd, nulla sebességgel.
    /// </summary>
    public void SetPosition(double x, double y) => _stateMachine.SetPosition(x, y);

    public void Update(
        TimeSpan deltaTime,
        IReadOnlyList<PlatformSegment> platforms,
        double cursorX,
        double cursorY,
        bool isMouseDown,
        int characterWidth,
        int characterHeight)
    {
        _stateMachine.Step(new MovementInput(
            deltaTime,
            platforms,
            cursorX,
            cursorY,
            isMouseDown,
            characterWidth,
            characterHeight));
    }
}
