namespace ConfigurationEngine.View;

/// <summary>
/// A tallózó párbeszédablakok (gui.md, "Tallózás"): fájl (<c>OpenFileDialog</c>) és mappa (<c>OpenFolderDialog</c>).
/// Külön interfész, hogy a Források fül nézetmodellje párbeszédablakok nélkül, egységtesztben is futtatható legyen.
/// </summary>
public interface IMediaBrowser
{
    /// <summary>Egy fájl kiválasztása a megadott kiterjesztésekkel (pl. <c>.gif</c>); <c>null</c>, ha a felhasználó mégsem választott.</summary>
    string? PickFile(IReadOnlyCollection<string> extensions);

    /// <summary>Egy mappa kiválasztása; <c>null</c>, ha a felhasználó mégsem választott.</summary>
    string? PickFolder();
}
