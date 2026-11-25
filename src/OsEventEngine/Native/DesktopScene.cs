namespace OsEventEngine.Native;

using MonkeyStack.Core.Character;

/// <summary>
/// Egy felső szintű ablak geometriája fizikai pixelben (a DWM kiterjesztett kerete, a láthatatlan átméretező keret nélkül).
/// </summary>
/// <param name="Bounds">Az ablak látható téglalapja.</param>
/// <param name="IsMaximized">Maximalizált-e (<c>IsZoomed</c>).</param>
internal readonly record struct SceneWindow(ScreenRect Bounds, bool IsMaximized);

/// <summary>
/// Egy monitor fizikai pixelben, és a fizikai pixel → WPF egység (DIP) váltószáma.
/// </summary>
/// <param name="Bounds">A teljes monitor.</param>
/// <param name="WorkArea">A munkaterület (a tálca nélkül).</param>
/// <param name="Scale">Fizikai pixel / DIP (pl. 1,5 a 150%-os skálázásnál).</param>
internal readonly record struct SceneMonitor(ScreenRect Bounds, ScreenRect WorkArea, double Scale);

/// <summary>
/// Az asztal egy pillanatképe a platform-számításhoz: az ablakok Z-sorrendben (elölről hátra) és a monitorok.
/// A <see cref="Platforms.PlatformCalculator"/> csak ezt látja, így natív hívás nélkül tesztelhető.
/// </summary>
internal sealed record DesktopScene(IReadOnlyList<SceneWindow> Windows, IReadOnlyList<SceneMonitor> Monitors);

/// <summary>
/// Az asztal lekérdezése (Wrapper Facade). Tesztekben hamis megvalósítás helyettesíti.
/// </summary>
internal interface IDesktopScanner
{
    DesktopScene Scan();
}
