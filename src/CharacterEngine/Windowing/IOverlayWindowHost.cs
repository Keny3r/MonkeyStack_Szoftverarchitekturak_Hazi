namespace CharacterEngine.Windowing;

using System.Windows.Controls;

/// <summary>
/// Az átlátszó, mindig legfelül lévő overlay ablak és az egérbemenet becsomagolásának interfésze (Wrapper Facade minta).
/// A Karakter View erre rajzol (<see cref="Canvas"/>), a fizika pedig innen kapja az egér állapotát (<see cref="GetInput"/>).
/// </summary>
public interface IOverlayWindowHost : IDisposable
{
    /// <summary>
    /// A rajzfelület. Képernyő-koordinátában (DIP) lehet rá rajzolni: az ablak helyéből adódó eltolást a host maga
    /// alkalmazza a Canvasra, így a renderelőnek nem kell tudnia róla.
    /// </summary>
    Canvas Canvas { get; }

    /// <summary>Az ablak aktuális helye és mérete (az összes monitort lefedi). Csak a UI szálon olvasható.</summary>
    OverlayLayout Layout { get; }

    /// <summary>
    /// A kurzor helye képernyő-koordinátában (DIP) és az elsődleges egérgomb állapota. Bármelyik szálról hívható; a
    /// <c>RenderManager</c> bemenet-szolgáltatójaként adható át.
    /// </summary>
    (double cursorX, double cursorY, bool isMouseDown) GetInput();

    /// <summary>Megjeleníti az ablakot. A UI szálon kell hívni.</summary>
    void Show();
}
