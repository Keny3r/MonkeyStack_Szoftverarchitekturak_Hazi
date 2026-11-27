namespace CharacterEngine.Tests.Fakes;

using CharacterEngine.StateMachine;
using MonkeyStack.Core.Character;

/// <summary>
/// Rögzíti a kapott hívásokat, a tulajdonságai pedig tetszőlegesen beállíthatók. A <see cref="CharacterPhysicsModel"/>
/// homlokzat tesztjeihez: azt ellenőrizzük vele, hogy a homlokzat mindent változatlanul ad tovább.
/// </summary>
public class FakeMovementStateMachine : IMovementStateMachine
{
    public double X { get; set; }

    public double Y { get; set; }

    public MovementState State { get; set; } = MovementState.Falling;

    public bool IsFacingLeft { get; set; }

    public MovementInput? LastInput { get; private set; }

    public (double X, double Y)? LastSetPosition { get; private set; }

    public void SetPosition(double x, double y) => LastSetPosition = (x, y);

    public void Step(MovementInput input) => LastInput = input;
}
