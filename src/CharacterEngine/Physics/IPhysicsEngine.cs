namespace CharacterEngine.Physics;

using MonkeyStack.Core.Character;

/// <summary>
/// A karakter belső fizikai motorjának interfésze (gravitáció, ütközésvizsgálat, platform-ellenőrzés).
/// Csak állapot nélküli számításokat tartalmaz: a pozíciót, a sebességet és a mozgásállapotot a
/// mozgási állapotgép tárolja, ez az interfész csak kiszámolja, mi történik egy lépésben.
/// Minden koordináta képernyő-koordináta (WPF egység, DIP), és lefelé nő az Y.
/// A karakter pozíciója a talppontja (alsó-középső pont), lásd <see cref="ICharacterPhysicsModel"/>.
/// </summary>
public interface IPhysicsEngine
{
    /// <summary>A motor fix paraméterei.</summary>
    PhysicsSettings Settings { get; }

    /// <summary>
    /// Egy lépésnyi esés: a sebességet a gravitációval növeli (legfeljebb
    /// <see cref="PhysicsSettings.MaxFallSpeedPxPerSec"/>-ig), majd az új sebességgel lépteti a pozíciót.
    /// A túl hosszú <paramref name="deltaTime"/> levágása a hívó dolga (<see cref="PhysicsSettings.MaxDeltaTime"/>).
    /// </summary>
    /// <param name="y">A talppont jelenlegi Y koordinátája.</param>
    /// <param name="velocityY">A jelenlegi függőleges sebesség (pixel / másodperc, lefelé pozitív).</param>
    /// <param name="deltaTime">A lépés hossza. Nulla vagy negatív érték esetén nincs mozgás.</param>
    /// <returns>Az új Y koordináta és az új sebesség.</returns>
    (double Y, double VelocityY) ApplyGravity(double y, double velocityY, TimeSpan deltaTime);

    /// <summary>
    /// Megkeresi azt a platformot, amelyre a karakter a <paramref name="previousY"/> → <paramref name="newY"/>
    /// esés közben ráérkezik. A teljes elmozdulást vizsgálja, nem csak az új pozíciót, így nagy sebességnél
    /// sem esik át a vékony ablakperemeken (docs/mvc_architecture.md, "Esés"). Több találatnál a legfelső nyer.
    /// </summary>
    /// <returns>A platform, amelyre a karakter landol, vagy <c>null</c>, ha nincs ilyen (vagy a karakter nem lefelé mozog).</returns>
    PlatformSegment? FindLandingPlatform(double x, double previousY, double newY, IReadOnlyList<PlatformSegment> platforms);

    /// <summary>
    /// Megkeresi azt a platformot, amelyen a karakter éppen áll: a platform legfeljebb
    /// <see cref="PhysicsSettings.SupportTolerancePx"/> távolságra van a talpponttól, és az X a platformon belül van.
    /// Az Idle és a Walking állapot minden lépésben ezzel ellenőrzi, hogy nem tűnt-e el a platform a karakter alól.
    /// </summary>
    /// <returns>A legközelebbi alátámasztó platform, vagy <c>null</c>, ha a karakter alatt nincs platform.</returns>
    PlatformSegment? FindSupportingPlatform(double x, double y, IReadOnlyList<PlatformSegment> platforms);

    /// <summary>
    /// Igaz, ha a kurzor a karakter befoglaló téglalapján belül van
    /// (<c>[x - width/2, x + width/2] × [y - height, y]</c>, a széleket is beleértve). A megragadás (Dragging) indításához.
    /// </summary>
    bool HitTest(double x, double y, int width, int height, double cursorX, double cursorY);
}
