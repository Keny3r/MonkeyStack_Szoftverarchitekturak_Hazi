namespace CharacterEngine.Physics;

/// <summary>
/// A karakter fizikájának fix paraméterei (docs/mvc_architecture.md, "Karakter és Fizika").
/// A Konfigurációs ablakban nem állíthatók (docs/configuration_engine/view.md); a sétasebesség és a gravitáció
/// alapértéke megegyezik a konfigurációs séma <c>CharacterSettings</c> táblájáéval (docs/configuration_engine/schema.md).
/// Megváltoztathatatlan, így a fizikai motor és az állapotgép szálbiztosan megoszthatja; a tesztek a
/// <c>with</c> kifejezéssel saját értékeket adhatnak meg.
/// </summary>
/// <remarks>
/// A platform szélétől tartott ráhagyás (<see cref="CharacterPhysicsModel.PlatformEdgeMargin"/>) és a tétlen állapot
/// hossza (<see cref="CharacterPhysicsModel.IdleDuration"/>) a <see cref="CharacterPhysicsModel"/>-ben marad, mert a
/// közös MVC terv ott hivatkozik rájuk.
/// </remarks>
public sealed record PhysicsSettings
{
    /// <summary>Az alapértelmezett, a futó alkalmazás által használt paraméterek.</summary>
    public static PhysicsSettings Default { get; } = new();

    /// <summary>Gravitáció, pixel / másodperc². A képernyőn lefelé nő az Y, ezért pozitív.</summary>
    public double GravityPxPerSec2 { get; init; } = 2000.0;

    /// <summary>Séta sebessége, pixel / másodperc.</summary>
    public double WalkSpeedPxPerSec { get; init; } = 120.0;

    /// <summary>
    /// A legnagyobb esési sebesség, pixel / másodperc. Enélkül hosszú esésnél a sebesség korlátlanul nőne.
    /// </summary>
    public double MaxFallSpeedPxPerSec { get; init; } = 1500.0;

    /// <summary>
    /// Egy lépésben legfeljebb ennyi időt szimulálunk. Ha egy tick sokáig késik (pl. debugger-szünet, akadás),
    /// a karakter így nem ugrik át egyszerre nagy távolságot.
    /// </summary>
    public TimeSpan MaxDeltaTime { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Ekkora függőleges eltérésen belül számít úgy, hogy a karakter talpa alatt platform van (pixel).
    /// Ha egy ablakot ennél többel mozdítanak el, a karakter leesik (docs/os_event_engine/platform_provider_proposal.md, 0. Döntések).
    /// </summary>
    public double SupportTolerancePx { get; init; } = 1.0;
}
