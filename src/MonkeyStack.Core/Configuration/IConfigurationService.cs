namespace MonkeyStack.Core.Configuration;

using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

/// <summary>
/// A Konfigurációs motor által biztosított állapotmentes lekérdező interfész (Component Configurator minta).
/// A Konfigurációs motor egy szabály-kiértékelő ("számítógép"): a Karakter állapotgép által átadott
/// paraméterek alapján visszaadja a megfelelő, már pörgetett és fix méretűre skálázott animációkat.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Esemény, amely akkor tüzel, ha a felhasználó a Config menüben módosítja a szabályokat,
    /// az animációkat vagy a képméretet, így a Karakter azonnal újra tudja kérdezni az animációkat.
    /// </summary>
    event EventHandler? ConfigurationChanged;

    /// <summary>
    /// A képek egységes skálázási szélessége pixelben (alapértelmezetten pl. 100).
    /// </summary>
    int CharacterWidth { get; set; }

    /// <summary>
    /// A képek egységes skálázási magassága pixelben (alapértelmezetten pl. 100).
    /// </summary>
    int CharacterHeight { get; set; }

    /// <summary>
    /// Kiértékeli a viselkedési szabályokat a kapott rendszeresemény-állapotok (OsEventDataRegistry) alapján,
    /// és Z-pozíció (ZIndex) szerint növekvő sorrendben visszaadja az aktív overlay animációk listáját.
    /// </summary>
    IReadOnlyList<IAnimation> GetOverlayAnimations(OsEventDataRegistry registry);

    /// <summary>
    /// Visszaadja a megadott mozgásállapothoz (Idle, Walking, Falling, Dragging) konfigurált 1 db animációt.
    /// </summary>
    IAnimation? GetMovementAnimation(MovementState state);
}
