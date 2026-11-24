namespace CharacterEngine.Rendering;

using MonkeyStack.Core.Animation;

/// <summary>
/// A szimulációs ciklus és a renderelés közötti szálbiztos adatátvitel immutábilis pillanatképe
/// (Half Sync / Half Async minta).
/// Minden szimulációs tickben egy új példány jön létre a legfrissebb pozícióval és animációkkal,
/// amelyet a WPF UI szál zármentesen vagy szálbiztosan vehet át kirajzolásra.
/// </summary>
/// <param name="X">A karakter talppontjának (alsó-középső pont) vízszintes koordinátája.</param>
/// <param name="Y">A karakter talppontjának (alsó-középső pont) függőleges koordinátája.</param>
/// <param name="IsFacingLeft">Ha igaz, a karakter balra néz (vízszintesen tükrözve rajzolandó).</param>
/// <param name="MovementAnimation">A karakter aktuális mozgásállapotához tartozó alap-animáció.</param>
/// <param name="OverlayAnimations">A karakter aktuális környezeti állapotaihoz tartozó aktív overlay animációk Z-index szerint.</param>
/// <param name="IsVisible">Jelzi, hogy a karakter elemei megjelenítendők-e a felületen.</param>
public sealed record CharacterRenderSnapshot(
    double X,
    double Y,
    bool IsFacingLeft,
    IAnimation? MovementAnimation,
    IReadOnlyList<IAnimation> OverlayAnimations,
    bool IsVisible = true)
{
    /// <summary>
    /// Üres, nem megjelenítendő pillanatkép (kezdeti állapot, amíg az első szimulációs lépés le nem fut).
    /// </summary>
    public static CharacterRenderSnapshot Empty { get; } = new(
        X: 0.0,
        Y: 0.0,
        IsFacingLeft: false,
        MovementAnimation: null,
        OverlayAnimations: Array.Empty<IAnimation>(),
        IsVisible: false);
}
