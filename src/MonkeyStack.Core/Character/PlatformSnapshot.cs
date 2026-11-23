namespace MonkeyStack.Core.Character;

/// <summary>
/// A járható felületek egy pillanatképe. Megváltoztathatatlan, így szálbiztosan olvasható.
/// </summary>
/// <param name="Platforms">
/// Ablakperemek (a takart részek nélkül) és monitoronként a talaj: a tálca teteje, fullscreen ablak esetén a képernyő alja.
/// A 20 px-nél keskenyebb szakaszok már ki vannak szűrve.
/// </param>
/// <param name="Screens">A monitorok állapota.</param>
/// <param name="Version">Minden újraszámolásnál nő; a fogyasztó ebből látja, hogy változott-e valami.</param>
public sealed record PlatformSnapshot(
    IReadOnlyList<PlatformSegment> Platforms,
    IReadOnlyList<ScreenArea> Screens,
    long Version)
{
    /// <summary>Üres pillanatkép: még nincs adat (pl. a szolgáltató nem indult el, vagy egy hamis szolgáltató kezdőértéke).</summary>
    public static PlatformSnapshot Empty { get; } = new([], [], 0);
}
