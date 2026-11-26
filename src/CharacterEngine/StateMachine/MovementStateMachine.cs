namespace CharacterEngine.StateMachine;

using CharacterEngine.Physics;
using MonkeyStack.Core.Character;

/// <summary>
/// A karakter mozgási állapotgépe (docs/mvc_architecture.md, állapotdiagram).
/// Kezeli a Falling, Idle, Walking és Dragging állapotok közötti átmeneteket és a nézési irányt.
/// Az időt nem a rendszerórából, hanem a lépések <see cref="MovementInput.DeltaTime"/>-jainak összegéből méri, így a
/// viselkedése tesztben is valós várakozás nélkül ellenőrizhető.
/// </summary>
/// <remarks>
/// Egy lépésben legfeljebb egy állapotváltás történik: a váltás utáni állapot viselkedése a következő lépésben indul
/// (pl. a platform eltűnése után a karakter a következő lépésben kezd esni). 60 FPS mellett ez ~16 ms késés.
/// </remarks>
public class MovementStateMachine : IMovementStateMachine
{
    private readonly IPhysicsEngine _physics;
    private readonly Func<bool> _chooseFacingLeft;

    /// <summary>A függőleges sebesség (pixel / másodperc, lefelé pozitív). Belső állapot, csak esés közben nem nulla.</summary>
    private double _velocityY;

    /// <summary>Mióta tétlen a karakter (az Idle → Walking átmenethez). Minden landoláskor nullázódik.</summary>
    private TimeSpan _idleElapsed;

    /// <param name="physics">A fizikai számításokat végző motor.</param>
    /// <param name="chooseFacingLeft">
    /// Az Idle → Walking átmenet véletlen kezdőiránya: igaz esetén balra indul. Alapértelmezetten egyenletes véletlen;
    /// a tesztek rögzített értéket adhatnak meg.
    /// </param>
    public MovementStateMachine(IPhysicsEngine physics, Func<bool>? chooseFacingLeft = null)
    {
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        _chooseFacingLeft = chooseFacingLeft ?? (static () => Random.Shared.Next(2) == 0);
    }

    public double X { get; private set; }

    public double Y { get; private set; }

    public MovementState State { get; private set; } = MovementState.Falling;

    public bool IsFacingLeft { get; private set; }

    public void SetPosition(double x, double y)
    {
        X = x;
        Y = y;
        StartFalling();
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

            case MovementState.Idle:
                StepIdle(deltaTime, input.Platforms);
                break;

            case MovementState.Walking:
                StepWalking(deltaTime, input.Platforms);
                break;

            // Dragging: a következő lépésben (A8).
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

    private void StepIdle(TimeSpan deltaTime, IReadOnlyList<PlatformSegment> platforms)
    {
        // Előbb az alátámasztás: ha az ablakot elmozdították vagy bezárták alóla, az időzítőtől függetlenül leesik.
        if (!TryStayOnPlatform(platforms, out _))
        {
            return;
        }

        _idleElapsed += deltaTime;
        if (_idleElapsed >= CharacterPhysicsModel.IdleDuration)
        {
            IsFacingLeft = _chooseFacingLeft();
            State = MovementState.Walking;
        }
    }

    private void StepWalking(TimeSpan deltaTime, IReadOnlyList<PlatformSegment> platforms)
    {
        // Mint Idle-ben: ha az ablakot elmozdították, bezárták vagy egy másik ablak kitakarta a karakter alatti részt, leesik.
        if (!TryStayOnPlatform(platforms, out var platform))
        {
            return;
        }

        double leftBound = platform.LeftX + CharacterPhysicsModel.PlatformEdgeMargin;
        double rightBound = platform.RightX - CharacterPhysicsModel.PlatformEdgeMargin;

        // A ráhagyásnál keskenyebb platformon nincs hová sétálni: a karakter egy helyben marad, és nem fordul meg
        // minden lépésben (docs/mvc_architecture.md, "Séta"). A platform-szolgáltató ezeket eleve kiszűri, ez csak védelem.
        if (leftBound > rightBound)
        {
            return;
        }

        double direction = IsFacingLeft ? -1.0 : 1.0;
        double newX = X + (direction * _physics.Settings.WalkSpeedPxPerSec * deltaTime.TotalSeconds);

        // A határon megfordul. Ha a karakter már a ráhagyás sávjában volt (pl. a szél közelében landolt), nem ugrik a
        // határra, hanem helyben fordul meg, és onnan sétál vissza.
        if (IsFacingLeft && newX <= leftBound)
        {
            newX = Math.Min(X, leftBound);
            IsFacingLeft = false;
        }
        else if (!IsFacingLeft && newX >= rightBound)
        {
            newX = Math.Max(X, rightBound);
            IsFacingLeft = true;
        }

        X = newX;
    }

    /// <summary>
    /// Ellenőrzi, hogy a karakter talpa alatt van-e még platform. Ha van, a talppontot pontosan a peremre igazítja
    /// (a tűrésen belüli, pl. fél pixeles elmozdulást követi); ha nincs, esésbe váltja a karaktert.
    /// </summary>
    /// <param name="platforms">Az aktuális platformok.</param>
    /// <param name="support">A platform, amelyen a karakter áll (csak igaz visszatérési értéknél érvényes).</param>
    /// <returns>Igaz, ha a karakter a platformon maradt.</returns>
    private bool TryStayOnPlatform(IReadOnlyList<PlatformSegment> platforms, out PlatformSegment support)
    {
        if (_physics.FindSupportingPlatform(X, Y, platforms) is { } found)
        {
            support = found;
            Y = found.Y;
            return true;
        }

        support = default;
        StartFalling();
        return false;
    }

    private void StartFalling()
    {
        _velocityY = 0.0;
        State = MovementState.Falling;
    }

    private void Land(PlatformSegment platform)
    {
        Y = platform.Y;
        _velocityY = 0.0;
        _idleElapsed = TimeSpan.Zero;
        State = MovementState.Idle;
    }
}
