namespace MonkeyStack.Core.Timing;

/// <summary>
/// Közös, egyenletes ütemű léptető (docs/character_engine/architecture.md, "Javasolt közös bővítések a
/// <c>MonkeyStack.Core</c>-ban"). A host egy példányt hoz létre, és ezt adja minden komponensnek, amelynek az idővel
/// lépnie kell: a Konfigurációs motor ebből lépteti az animációkat, a Karakter motor a fizikát.
/// </summary>
public interface IFrameLoop
{
    /// <summary>
    /// Ütemenként egyszer tüzel, az előző ütem óta eltelt idővel. A léptető saját szálán váltódik ki, nem a UI szálon,
    /// ezért a feliratkozónak szálbiztosnak kell lennie, és gyorsan vissza kell térnie. Egy feliratkozó kivétele nem
    /// éri el a többit, és nem állítja le a léptetőt.
    /// </summary>
    event EventHandler<FrameTickEventArgs>? Tick;
}
