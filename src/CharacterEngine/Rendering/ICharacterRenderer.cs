namespace CharacterEngine.Rendering;

using System.Windows.Controls;

/// <summary>
/// A karakter alap-animációjának és overlay-einek kirajzolásáért felelős komponens interfésze.
/// A <see cref="CharacterRenderSnapshot"/> alapján kirajzolja a képkockákat a cél Canvas-ra.
/// </summary>
public interface ICharacterRenderer
{
    /// <summary>
    /// Kirajzolja a karakter mozgás- és overlay animációit a megadott pillanatkép alapján.
    /// </summary>
    /// <param name="targetCanvas">A cél WPF Canvas, ahova a karakter elemei kerülnek.</param>
    /// <param name="snapshot">A szálbiztos pillanatkép a pozícióval és animációkkal.</param>
    void Render(Canvas targetCanvas, CharacterRenderSnapshot snapshot);

    /// <summary>
    /// Elrejti az összes aktív AnimationView elemet.
    /// </summary>
    void HideAll();

    /// <summary>
    /// Eltávolítja az összes AnimationView elemet a cél Canvas-ról (erőforrás-takarítás újratöltéskor).
    /// </summary>
    void DetachAll();
}
