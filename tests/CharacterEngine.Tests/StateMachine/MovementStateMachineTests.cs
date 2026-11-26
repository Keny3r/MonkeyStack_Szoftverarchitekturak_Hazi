namespace CharacterEngine.Tests.StateMachine;

using CharacterEngine.Physics;
using CharacterEngine.StateMachine;
using MonkeyStack.Core.Character;

/// <summary>
/// Az állapotgép a valódi <see cref="PhysicsEngine"/>-nel fut: a motor tiszta függvényei külön tesztelve vannak, egy hamis
/// motor itt csak ugyanazt a számolást írná le újra. A tesztek csak a publikus viselkedést (pozíció, állapot, irány)
/// ellenőrzik; a sebesség belső állapot, ezért az elmozdulásból következtetünk rá.
/// </summary>
public class MovementStateMachineTests
{
    private const int Precision = 9;

    // Kerek számok: nyugalomból indulva az első 0,1 s-os lépésben pontosan 10 px-t esik (100 px/s-re gyorsul),
    // a másodikban 20 px-t (200 px/s).
    private const double FirstStepFromRest = 10.0;
    private const double SecondStepFromRest = 20.0;

    private static readonly PhysicsSettings TestSettings = PhysicsSettings.Default with
    {
        GravityPxPerSec2 = 1000.0,
        MaxFallSpeedPxPerSec = 500.0,
        MaxDeltaTime = TimeSpan.FromMilliseconds(100),
    };

    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    private readonly MovementStateMachine _machine = new(new PhysicsEngine(TestSettings));

    private static MovementInput Input(TimeSpan deltaTime, params PlatformSegment[] platforms) =>
        new(deltaTime, platforms, CursorX: 0, CursorY: 0, IsMouseDown: false, CharacterWidth: 40, CharacterHeight: 60);

    /// <summary>Egy lépést tesz, és visszaadja, mennyit mozdult lefelé a karakter.</summary>
    private double StepAndMeasureFall(params PlatformSegment[] platforms)
    {
        double before = _machine.Y;
        _machine.Step(Input(Tick, platforms));
        return _machine.Y - before;
    }

    [Fact]
    public void NewMachine_StartsFallingAtOriginFacingRight()
    {
        Assert.Equal(MovementState.Falling, _machine.State);
        Assert.Equal(0.0, _machine.X);
        Assert.Equal(0.0, _machine.Y);
        Assert.False(_machine.IsFacingLeft);
    }

    // --- Falling ---

    [Fact]
    public void Falling_StartsFromRestAndAccelerates()
    {
        // Ha nem nyugalomból indulna, az első lépés többet esne; ha nem gyorsulna, a második nem lenne nagyobb.
        Assert.Equal(FirstStepFromRest, StepAndMeasureFall(), Precision);
        Assert.Equal(SecondStepFromRest, StepAndMeasureFall(), Precision);
        Assert.Equal(MovementState.Falling, _machine.State);
    }

    [Fact]
    public void Falling_ReachesPlatform_SnapsOntoItAndBecomesIdle()
    {
        _machine.SetPosition(x: 50, y: 95);
        var platform = new PlatformSegment(Y: 100, LeftX: 0, RightX: 200);

        _machine.Step(Input(Tick, platform)); // 95 → 105 lenne, de a 100-as peremen megáll

        Assert.Equal(MovementState.Idle, _machine.State);
        Assert.Equal(100.0, _machine.Y);
        Assert.Equal(50.0, _machine.X);
    }

    [Fact]
    public void Falling_PlatformNotUnderCharacter_KeepsFalling()
    {
        _machine.SetPosition(x: 300, y: 95);
        var platform = new PlatformSegment(Y: 100, LeftX: 0, RightX: 200);

        _machine.Step(Input(Tick, platform));

        Assert.Equal(MovementState.Falling, _machine.State);
        Assert.Equal(105.0, _machine.Y, Precision);
    }

    [Fact]
    public void Step_LongDeltaTime_IsClampedToMaxDeltaTime()
    {
        // Egy 10 másodperces akadás után is csak 0,1 s-nyit esik, nem ugrik át mindenen.
        _machine.Step(Input(TimeSpan.FromSeconds(10)));

        Assert.Equal(FirstStepFromRest, _machine.Y, Precision);
    }

    [Fact]
    public void Step_NonPositiveDeltaTime_DoesNotMove()
    {
        _machine.SetPosition(x: 50, y: 20);

        _machine.Step(Input(TimeSpan.FromMilliseconds(-5)));

        Assert.Equal(20.0, _machine.Y);
        Assert.Equal(MovementState.Falling, _machine.State);
    }

    // --- SetPosition ---

    [Fact]
    public void SetPosition_WhileFallingFast_RestartsFallFromRest()
    {
        StepAndMeasureFall();
        StepAndMeasureFall(); // már 200 px/s-mal esik

        _machine.SetPosition(x: 10, y: 20);

        Assert.Equal(10.0, _machine.X);
        Assert.Equal(20.0, _machine.Y);
        Assert.Equal(MovementState.Falling, _machine.State);

        // Ha a sebesség megmaradt volna, ez a lépés 30 px lenne.
        Assert.Equal(FirstStepFromRest, StepAndMeasureFall(), Precision);
    }

    [Fact]
    public void SetPosition_ExactlyOnPlatform_LandsOnNextStep()
    {
        var platform = new PlatformSegment(Y: 100, LeftX: 0, RightX: 200);
        _machine.SetPosition(x: 50, y: 100);

        _machine.Step(Input(Tick, platform));

        Assert.Equal(MovementState.Idle, _machine.State);
        Assert.Equal(100.0, _machine.Y);
    }
}
