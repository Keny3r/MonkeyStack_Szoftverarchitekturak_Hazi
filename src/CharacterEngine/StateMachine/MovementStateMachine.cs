namespace CharacterEngine.StateMachine;

using CharacterEngine.Physics;
using MonkeyStack.Core.Character;

/// <summary>
/// A karakter mozgási állapotgépe (docs/mvc_architecture.md, állapotdiagram).
/// Kezeli a Falling, Idle, Walking és Dragging állapotok közötti átmeneteket és a nézési irányt.
/// Az időt nem a rendszerórából, hanem a lépések <see cref="MovementInput.DeltaTime"/>-jainak összegéből méri, így a
/// viselkedése tesztben is valós várakozás nélkül ellenőrizhető.
/// </summary>
public class MovementStateMachine : IMovementStateMachine
{
    private readonly IPhysicsEngine _physics;

    /// <summary>A függőleges sebesség (pixel / másodperc, lefelé pozitív). Belső állapot, csak esés közben nem nulla.</summary>
    private double _velocityY;

    public MovementStateMachine(IPhysicsEngine physics)
    {
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
    }

    public double X { get; private set; }

    public double Y { get; private set; }

    public MovementState State { get; private set; } = MovementState.Falling;

    public bool IsFacingLeft { get; private set; }

    public void SetPosition(double x, double y)
    {
        X = x;
        Y = y;
        _velocityY = 0.0;
        State = MovementState.Falling;
    }

    public void Step(MovementInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Platforms);

        TimeSpan deltaTime = ClampDeltaTime(input.DeltaTime);

        switch (State)
        {
            case MovementState.Falling:
                StepFalling(deltaTime, input.Platforms);
                break;

            // Idle, Walking, Dragging: a következő lépésekben (A6–A8).
            default:
                break;
        }
    }

    private TimeSpan ClampDeltaTime(TimeSpan deltaTime)
    {
        if (deltaTime <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        TimeSpan max = _physics.Settings.MaxDeltaTime;
        return deltaTime > max ? max : deltaTime;
    }

    private void StepFalling(TimeSpan deltaTime, IReadOnlyList<PlatformSegment> platforms)
    {
        double previousY = Y;
        var (newY, newVelocity) = _physics.ApplyGravity(Y, _velocityY, deltaTime);

        var landing = _physics.FindLandingPlatform(X, previousY, newY, platforms);
        if (landing is { } platform)
        {
            Land(platform);
            return;
        }

        Y = newY;
        _velocityY = newVelocity;
    }

    private void Land(PlatformSegment platform)
    {
        Y = platform.Y;
        _velocityY = 0.0;
        State = MovementState.Idle;
    }
}
