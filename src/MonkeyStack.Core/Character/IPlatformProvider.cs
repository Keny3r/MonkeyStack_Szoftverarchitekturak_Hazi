namespace MonkeyStack.Core.Character;

/// <summary>
/// A járható felületek (ablakperemek, tálca, képernyő alja) szolgáltatója a fizikai motor számára.
/// Az OS esemény értesítő motor valósítja meg (<c>Win32PlatformProvider</c>, M11), a host adja át a Karakternek.
/// Elfogadva a #3-as PR-ben: docs/os_event_engine/platform_provider_proposal.md.
/// </summary>
/// <remarks>
/// A fizika minden tickben a <see cref="Current"/>-et olvassa, és a <see cref="PlatformSnapshot.Platforms"/>
/// listát adja át az <see cref="ICharacterPhysicsModel.Update"/>-nek. Ha az ablak, amin a karakter áll, eltűnik,
/// maximalizálódik vagy fullscreenbe megy, a platformja kikerül a listából, és a karakter leesik.
/// </remarks>
public interface IPlatformProvider
{
    /// <summary>
    /// A legutóbbi kész pillanatkép. Olcsó, lock nélküli olvasás: a ~60 FPS-es fizika minden tickben hívhatja.
    /// Indulás előtt <see cref="PlatformSnapshot.Empty"/>.
    /// </summary>
    PlatformSnapshot Current { get; }

    /// <summary>Akkor váltódik ki, ha a platformok változtak. Bármelyik szálon jöhet.</summary>
    event EventHandler<PlatformSnapshot>? PlatformsChanged;
}
