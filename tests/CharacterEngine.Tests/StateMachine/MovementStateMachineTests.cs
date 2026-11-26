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
        WalkSpeedPxPerSec = 120.0, // 0,1 s alatt 12 px
    };

    private const double WalkStep = 12.0;

    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    // Az Idle → Walking átmenet 2 s, azaz pontosan 20 darab 0,1 s-os lépés (a TimeSpan egész tickekben számol).
    private static readonly int TicksUntilWalking = (int)(CharacterPhysicsModel.IdleDuration / Tick);

    private static readonly PlatformSegment Floor = new(Y: 100, LeftX: 0, RightX: 200);

    private readonly MovementStateMachine _machine;

    /// <summary>A véletlen kezdőirány helyett ezt adja az állapotgép (a tesztek állítják).</summary>
    private bool _nextFacingLeft;

    public MovementStateMachineTests()
    {
        _machine = new MovementStateMachine(new PhysicsEngine(TestSettings), () => _nextFacingLeft);
    }

    private static MovementInput Input(TimeSpan deltaTime, params PlatformSegment[] platforms) =>
        new(deltaTime, platforms, CursorX: 0, CursorY: 0, IsMouseDown: false, CharacterWidth: 40, CharacterHeight: 60);

    /// <summary>Egy lépést tesz, és visszaadja, mennyit mozdult lefelé a karakter.</summary>
    private double StepAndMeasureFall(params PlatformSegment[] platforms)
    {
        double before = _machine.Y;
        _machine.Step(Input(Tick, platforms));
        return _machine.Y - before;
    }

    /// <summary>A karaktert a <see cref="Floor"/> platformra teszi, és landoltatja (Idle állapotba hozza).</summary>
    private void LandOnFloor(double x = 50)
    {
        _machine.SetPosition(x, Floor.Y);
        _machine.Step(Input(Tick, Floor));
        Assert.Equal(MovementState.Idle, _machine.State);
    }

    /// <summary>
    /// A karaktert a platformra teszi, landoltatja és kivárja az Idle időt, így a megadott irányba néző Walking állapotba
    /// kerül. Az átmenet lépésében még nem mozdul, ezért az X ekkor még a megadott érték.
    /// </summary>
    private void StartWalking(double x, bool facingLeft, PlatformSegment? platform = null)
    {
        var p = platform ?? Floor;
        _nextFacingLeft = facingLeft;
        _machine.SetPosition(x, p.Y);
        StepTimes(1 + TicksUntilWalking, p);

        Assert.Equal(MovementState.Walking, _machine.State);
        Assert.Equal(x, _machine.X);
    }

    private void StepTimes(int count, params PlatformSegment[] platforms)
    {
        for (int i = 0; i < count; i++)
        {
            _machine.Step(Input(Tick, platforms));
        }
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

    // --- Idle ---

    [Fact]
    public void Idle_BeforeIdleDuration_StaysInPlace()
    {
        LandOnFloor(x: 50);

        StepTimes(TicksUntilWalking - 1, Floor); // 1,9 s

        Assert.Equal(MovementState.Idle, _machine.State);
        Assert.Equal(50.0, _machine.X);
        Assert.Equal(Floor.Y, _machine.Y);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Idle_AfterIdleDuration_StartsWalkingInChosenDirection(bool facingLeft)
    {
        _nextFacingLeft = facingLeft;
        LandOnFloor();

        StepTimes(TicksUntilWalking, Floor); // 2,0 s

        Assert.Equal(MovementState.Walking, _machine.State);
        Assert.Equal(facingLeft, _machine.IsFacingLeft);
    }

    [Fact]
    public void Idle_PlatformDisappears_StartsFalling()
    {
        LandOnFloor();

        _machine.Step(Input(Tick)); // az ablakot bezárták: nincs platform

        Assert.Equal(MovementState.Falling, _machine.State);
    }

    [Fact]
    public void Idle_PlatformMovedBeyondTolerance_StartsFalling()
    {
        LandOnFloor();

        _machine.Step(Input(Tick, Floor with { Y = Floor.Y + 5 })); // az ablakot 5 px-szel lejjebb húzták

        Assert.Equal(MovementState.Falling, _machine.State);
    }

    [Fact]
    public void Idle_PlatformMovedWithinTolerance_StaysIdleAndFollowsEdge()
    {
        LandOnFloor();
        var shifted = Floor with { Y = Floor.Y + 0.5 };

        _machine.Step(Input(Tick, shifted));

        Assert.Equal(MovementState.Idle, _machine.State);
        Assert.Equal(shifted.Y, _machine.Y);
    }

    [Fact]
    public void Idle_FallingAgainAfterLanding_StartsFromRest()
    {
        // A landolás nullázza a sebességet: az újabb esés első lépése ugyanannyi, mint nyugalomból.
        _machine.SetPosition(x: 50, y: 0);
        StepTimes(9, Floor);                       // 0 → 100 px esés, közben gyorsul
        Assert.Equal(MovementState.Idle, _machine.State);

        _machine.Step(Input(Tick));                // eltűnik a platform → Falling

        Assert.Equal(FirstStepFromRest, StepAndMeasureFall(), Precision);
    }

    [Fact]
    public void Idle_TimerRestartsAfterEveryLanding()
    {
        var lower = new PlatformSegment(Y: 105, LeftX: 0, RightX: 200);
        LandOnFloor();
        StepTimes(TicksUntilWalking - 5, Floor);   // 1,5 s tétlenség

        _machine.Step(Input(Tick, lower));         // a felső eltűnik → Falling
        _machine.Step(Input(Tick, lower));         // 100 → 110 lenne, a 105-ös peremen landol
        Assert.Equal(MovementState.Idle, _machine.State);

        StepTimes(TicksUntilWalking - 1, lower);   // ha nem nullázódna, már rég sétálna
        Assert.Equal(MovementState.Idle, _machine.State);

        _machine.Step(Input(Tick, lower));
        Assert.Equal(MovementState.Walking, _machine.State);
    }

    // --- Walking ---
    // Floor: X 0..200, ráhagyás 10 px => a séta határai 10 és 190.

    [Theory]
    [InlineData(false, 100.0 + WalkStep)]
    [InlineData(true, 100.0 - WalkStep)]
    public void Walking_MovesInFacingDirectionAtWalkSpeed(bool facingLeft, double expectedX)
    {
        StartWalking(x: 100, facingLeft);

        _machine.Step(Input(Tick, Floor));

        Assert.Equal(expectedX, _machine.X, Precision);
        Assert.Equal(Floor.Y, _machine.Y);
        Assert.Equal(MovementState.Walking, _machine.State);
    }

    [Theory]
    [InlineData(185.0, false, 190.0)] // jobbra: 185 + 12 = 197 lenne, a 190-es határon megáll és megfordul
    [InlineData(15.0, true, 10.0)]    // balra: 15 - 12 = 3 lenne, a 10-es határon megáll és megfordul
    public void Walking_ReachesEdge_StopsAtMarginAndTurns(double startX, bool facingLeft, double expectedX)
    {
        StartWalking(startX, facingLeft);

        _machine.Step(Input(Tick, Floor));

        Assert.Equal(expectedX, _machine.X, Precision);
        Assert.Equal(!facingLeft, _machine.IsFacingLeft);
    }

    [Fact]
    public void Walking_AfterTurning_WalksBack()
    {
        StartWalking(x: 185, facingLeft: false);
        _machine.Step(Input(Tick, Floor)); // 190, megfordul

        _machine.Step(Input(Tick, Floor));

        Assert.Equal(190.0 - WalkStep, _machine.X, Precision);
        Assert.True(_machine.IsFacingLeft);
    }

    [Fact]
    public void Walking_StartedInsideEdgeMargin_TurnsInPlaceWithoutJumping()
    {
        // A szél közelében (a ráhagyás sávjában) landolt: nem ugrik a 10-es határra, hanem helyben megfordul.
        StartWalking(x: 3, facingLeft: true);

        _machine.Step(Input(Tick, Floor));
        Assert.Equal(3.0, _machine.X);
        Assert.False(_machine.IsFacingLeft);

        _machine.Step(Input(Tick, Floor));
        Assert.Equal(3.0 + WalkStep, _machine.X, Precision);
    }

    [Fact]
    public void Walking_PlatformNarrowerThanBothMargins_StaysPutWithoutFlipping()
    {
        // 15 px széles: a 2 x 10 px-es ráhagyás után nincs hová sétálni, ne forduljon minden lépésben.
        var narrow = new PlatformSegment(Y: 100, LeftX: 0, RightX: 15);
        StartWalking(x: 7, facingLeft: false, narrow);

        StepTimes(10, narrow);

        Assert.Equal(7.0, _machine.X);
        Assert.False(_machine.IsFacingLeft);
        Assert.Equal(MovementState.Walking, _machine.State);
    }

    [Fact]
    public void Walking_PlatformDisappears_StartsFalling()
    {
        StartWalking(x: 100, facingLeft: false);

        _machine.Step(Input(Tick));

        Assert.Equal(MovementState.Falling, _machine.State);
    }

    [Fact]
    public void Walking_LongWalk_StaysWithinMarginsAndKeepsTurning()
    {
        // 30 s séta: soha nem hagyja el a határokat, és oda-vissza jár (nem ragad be egy helyre).
        StartWalking(x: 100, facingLeft: false);
        int turns = 0;

        for (int i = 0; i < 300; i++)
        {
            bool before = _machine.IsFacingLeft;
            _machine.Step(Input(Tick, Floor));

            Assert.InRange(_machine.X, 10.0, 190.0);
            if (_machine.IsFacingLeft != before)
            {
                turns++;
            }
        }

        Assert.Equal(MovementState.Walking, _machine.State);
        Assert.True(turns >= 10, $"Csak {turns} fordulás történt.");
    }
}
