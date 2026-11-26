namespace CharacterEngine.StateMachine;

using MonkeyStack.Core.Character;

/// <summary>
/// A karakter beépített mozgásállapotait (Falling, Idle, Walking, Dragging) kezelő állapotgép interfésze
/// (docs/mvc_architecture.md, "Karakter és Fizika"). Ez tárolja a karakter teljes mozgási állapotát (pozíció,
/// sebesség, nézési irány, időzítők); a számításokat az <see cref="Physics.IPhysicsEngine"/>-re bízza.
/// </summary>
/// <remarks>
/// Nem szálbiztos. Feltevés, amelyet még egyeztetni kell a Karakter motor másik felelősével: az állapotgépet a
/// <see cref="CharacterPhysicsModel"/> homlokzaton keresztül kizárólag a <c>RenderManager</c> éri el, a saját zárja alatt,
/// a többi szál (pl. a UI) pedig csak a <c>CharacterRenderSnapshot</c> pillanatképet olvassa.
/// Az állapotváltásról a koordinátort nem értesíti; ezt a <c>RenderManager</c> végzi, amikor a lépés után
/// eltér a <see cref="State"/> a koordinátor állapotától.
/// </remarks>
public interface IMovementStateMachine
{
    /// <summary>A talppont (alsó-középső pont) X koordinátája.</summary>
    double X { get; }

    /// <summary>A talppont (alsó-középső pont) Y koordinátája.</summary>
    double Y { get; }

    /// <summary>Az aktuális mozgásállapot.</summary>
    MovementState State { get; }

    /// <summary>Igaz, ha a karakter balra néz.</summary>
    bool IsFacingLeft { get; }

    /// <summary>
    /// A karakter áthelyezése (pl. a kezdőpozíció beállítása indításkor). A karakter innen esésbe kezd, nulla sebességgel;
    /// ha az új pozíció pontosan egy platformon van, a következő lépésben landol.
    /// </summary>
    void SetPosition(double x, double y);

    /// <summary>Egy szimulációs lépés végrehajtása.</summary>
    void Step(MovementInput input);
}
