namespace CharacterEngine.Rendering;

/// <summary>
/// A szimulációs réteg és a UI renderelő közötti szálbiztos (Thread-Safe Interface / Half Sync - Half Async)
/// adatátadásra szolgáló megváltoztathatatlan pillanatkép váza.
/// </summary>
public sealed record CharacterRenderSnapshot;
