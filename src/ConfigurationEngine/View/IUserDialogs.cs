namespace ConfigurationEngine.View;

/// <summary>
/// A felhasználó döntése, ha mentetlen módosítással zárja be az ablakot.
/// </summary>
public enum CloseDecision
{
    /// <summary>Mentés, majd bezárás.</summary>
    Save,

    /// <summary>A módosítások elvetése, majd bezárás.</summary>
    Discard,

    /// <summary>Az ablak nyitva marad.</summary>
    Cancel
}

/// <summary>
/// A nézetmodell által kért megerősítő párbeszédablakok. Külön interfész, hogy a nézetmodell
/// párbeszédablakok nélkül, egységtesztben is futtatható legyen.
/// </summary>
public interface IUserDialogs
{
    /// <summary>Megerősítést kér; igaz, ha a felhasználó jóváhagyta.</summary>
    bool Confirm(string title, string message);

    /// <summary>Megkérdezi, mi történjen a mentetlen módosításokkal az ablak bezárásakor.</summary>
    CloseDecision AskUnsavedChanges();
}
