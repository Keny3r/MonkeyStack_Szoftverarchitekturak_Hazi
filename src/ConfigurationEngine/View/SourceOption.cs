namespace ConfigurationEngine.View;

/// <summary>
/// Egy animációs forrás a forrásválasztó legördülő listákban (view.md, "Mozgási animációk" és "Részletek"):
/// a szerkesztett másolat forrásai, a még nem mentett, negatív ideiglenes azonosítójúakkal együtt.
/// </summary>
public sealed class SourceOption : ObservableObject
{
    private string _name;

    public SourceOption(int id, string name)
    {
        Id = id;
        _name = name;
    }

    /// <summary>A forrás azonosítója (új forrásnál negatív ideiglenes azonosító); ez kerül a hivatkozásba.</summary>
    public int Id { get; }

    /// <summary>A forrás neve, ahogy a Források fülön szerepel.</summary>
    public string Name
    {
        get => _name;
        internal set => SetProperty(ref _name, value);
    }
}
