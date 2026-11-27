namespace CharacterEngine.Tests;

using CharacterEngine.Physics;
using CharacterEngine.Tests.Fakes;
using MonkeyStack.Core.Character;

public class CharacterPhysicsModelTests
{
    [Fact]
    public void Update_PassesEveryArgumentUnchangedToStateMachine()
    {
        // Minden paraméter más értéket kap, így egy felcserélt paraméter (pl. cursorX / cursorY) azonnal kiderül.
        var stateMachine = new FakeMovementStateMachine();
        var model = new CharacterPhysicsModel(stateMachine);
        PlatformSegment[] platforms = [new(Y: 100, LeftX: 0, RightX: 200)];

        model.Update(TimeSpan.FromMilliseconds(16), platforms, cursorX: 11, cursorY: 22, isMouseDown: true,
            characterWidth: 33, characterHeight: 44);

        var input = Assert.NotNull(stateMachine.LastInput);
        Assert.Equal(TimeSpan.FromMilliseconds(16), input.DeltaTime);
        Assert.Same(platforms, input.Platforms);
        Assert.Equal(11.0, input.CursorX);
        Assert.Equal(22.0, input.CursorY);
        Assert.True(input.IsMouseDown);
        Assert.Equal(33, input.CharacterWidth);
        Assert.Equal(44, input.CharacterHeight);
    }

    [Fact]
    public void Properties_ReturnStateMachineValues()
    {
        var stateMachine = new FakeMovementStateMachine
        {
            X = 12.5,
            Y = 300.0,
            State = MovementState.Walking,
            IsFacingLeft = true,
        };

        var model = new CharacterPhysicsModel(stateMachine);

        Assert.Equal(12.5, model.X);
        Assert.Equal(300.0, model.Y);
        Assert.Equal(MovementState.Walking, model.State);
        Assert.True(model.IsFacingLeft);
    }

    [Fact]
    public void SetPosition_IsForwardedToStateMachine()
    {
        var stateMachine = new FakeMovementStateMachine();
        var model = new CharacterPhysicsModel(stateMachine);

        model.SetPosition(x: 640, y: 10);

        Assert.Equal((640.0, 10.0), stateMachine.LastSetPosition);
    }

    [Fact]
    public void DefaultConstructor_RunsTheRealPhysics()
    {
        // A paraméter nélküli konstruktor összekötése (valódi motor, alapértelmezett paraméterek, véletlen irány):
        // leejtjük, landol, 2 s után elindul, és a sétasebességgel halad. A részletes viselkedést az állapotgép tesztjei
        // ellenőrzik, itt csak azt, hogy a homlokzat mögött tényleg az fut.
        var model = new CharacterPhysicsModel();
        PlatformSegment[] platforms = [new(Y: 500, LeftX: 0, RightX: 1000)];
        var tick = TimeSpan.FromMilliseconds(50); // a TimeSpan-ben pontos: 40 lépés = 2 s
        void Step() => model.Update(tick, platforms, cursorX: -1000, cursorY: -1000, isMouseDown: false,
            characterWidth: 64, characterHeight: 64);

        model.SetPosition(x: 500, y: 0);
        Assert.Equal(MovementState.Falling, model.State);

        for (int i = 0; i < 100 && model.State == MovementState.Falling; i++)
        {
            Step();
        }

        Assert.Equal(MovementState.Idle, model.State);
        Assert.Equal(500.0, model.Y);

        int ticksUntilWalking = (int)(CharacterPhysicsModel.IdleDuration / tick);
        for (int i = 0; i < ticksUntilWalking; i++)
        {
            Step();
        }

        Assert.Equal(MovementState.Walking, model.State);

        double before = model.X;
        Step();
        double expectedStep = PhysicsSettings.Default.WalkSpeedPxPerSec * tick.TotalSeconds;
        Assert.Equal(expectedStep, Math.Abs(model.X - before), precision: 9);
        Assert.Equal(model.IsFacingLeft, model.X < before);
    }
}
