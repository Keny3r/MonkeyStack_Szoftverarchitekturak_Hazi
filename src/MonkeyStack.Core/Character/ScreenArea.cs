namespace MonkeyStack.Core.Character;

/// <summary>
/// Egy monitor állapota WPF egységben (DIP).
/// </summary>
/// <param name="Bounds">A teljes monitor.</param>
/// <param name="WorkArea">A munkaterület (tálca nélkül).</param>
/// <param name="HasFullscreenWindow">Van-e rajta a teljes monitort lefedő ablak. Csak tájékoztató: a karakter ilyenkor leesik a képernyő aljára, de nem rejtjük el.</param>
public sealed record ScreenArea(ScreenRect Bounds, ScreenRect WorkArea, bool HasFullscreenWindow);
