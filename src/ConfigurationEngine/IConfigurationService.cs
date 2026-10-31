namespace ConfigurationEngine;

using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

/// <summary>
/// A Konfigurációs motor által biztosított interfész a lekérdezésekhez (Component Configurator / Facade minta).
/// A konkrét osztályt és perzisztenciát a konfigurációs komponens felelőse implementálja.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Visszaadja a jelenlegi rendszeresemény-állapotokhoz konfigurált overlay animációt.
    /// </summary>
    IAnimation? GetOverlayAnimation(OsEventDataRegistry registry);

    /// <summary>
    /// Visszaadja a megadott mozgásállapothoz konfigurált animációt.
    /// </summary>
    IAnimation? GetMovementAnimation(MovementState state);
}
