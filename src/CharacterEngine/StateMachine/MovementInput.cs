namespace CharacterEngine.StateMachine;

using MonkeyStack.Core.Character;

/// <summary>
/// Egy szimulációs lépés bemenete a mozgási állapotgép számára. Ugyanazokat az adatokat fogja össze, amelyeket a
/// <see cref="ICharacterPhysicsModel.Update"/> kap, így az állapotgép metódusai egyetlen paramétert adnak tovább.
/// Minden koordináta képernyő-koordináta (WPF egység, DIP), lefelé nő az Y.
/// </summary>
/// <param name="DeltaTime">Az előző lépés óta eltelt idő.</param>
/// <param name="Platforms">Az aktuálisan járható felületek (ablakperemek, talaj).</param>
/// <param name="CursorX">A kurzor X koordinátája.</param>
/// <param name="CursorY">A kurzor Y koordinátája.</param>
/// <param name="IsMouseDown">Le van-e nyomva a bal egérgomb.</param>
/// <param name="CharacterWidth">A karakter szélessége pixelben (a megragadás találatvizsgálatához).</param>
/// <param name="CharacterHeight">A karakter magassága pixelben.</param>
public readonly record struct MovementInput(
    TimeSpan DeltaTime,
    IReadOnlyList<PlatformSegment> Platforms,
    double CursorX,
    double CursorY,
    bool IsMouseDown,
    int CharacterWidth,
    int CharacterHeight);
