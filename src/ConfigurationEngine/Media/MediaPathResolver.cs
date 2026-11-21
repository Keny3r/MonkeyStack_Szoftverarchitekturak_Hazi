namespace ConfigurationEngine.Media;

using System.IO;
using ConfigurationEngine.Model;

/// <summary>
/// Az animációs források útvonalának feloldása (schema.md, "Médiafájlok validálása"): a relatív útvonal
/// az alkalmazás <c>assets</c> mappájához képest értelmeződik, a hálózati és eszközútvonal nem megengedett.
/// A <see cref="MediaValidator"/> és a <see cref="FrameLoader"/> is ezt használja, így ugyanazt a fájlt nézik.
/// </summary>
public sealed class MediaPathResolver
{
    /// <param name="assetsDirectory">
    /// A szállított képek mappája; alapértelmezés szerint az alkalmazás mappájában lévő <c>assets</c>.
    /// A tesztek ideiglenes mappát adnak meg.
    /// </param>
    public MediaPathResolver(string? assetsDirectory = null)
    {
        AssetsDirectory = Path.GetFullPath(assetsDirectory ?? Path.Combine(AppContext.BaseDirectory, "assets"));
    }

    /// <summary>A relatív útvonalak alapja.</summary>
    public string AssetsDirectory { get; }

    /// <summary>
    /// A forrás útvonalát teljes, normalizált útvonallá alakítja.
    /// </summary>
    /// <exception cref="MediaLoadException">Ha az útvonal üres, hálózati, eszköz- vagy hiányos (pl. <c>C:kep.gif</c>).</exception>
    public string Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw PathError("Az útvonal megadása kötelező.");
        }

        // UNC (\\szerver\megosztás) és eszközútvonal (\\?\, \\.\) egyaránt két elválasztóval kezdődik.
        if (path.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1]))
        {
            throw PathError("Hálózati vagy eszközútvonal nem megengedett.");
        }

        if (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
        {
            throw PathError("Az útvonal legyen teljes (pl. C:\\Képek\\majom.gif), vagy az assets mappához képest relatív.");
        }

        try
        {
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(AssetsDirectory, path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw PathError("Az útvonal érvénytelen.", ex);
        }
    }

    private static bool IsSeparator(char c) => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

    private static MediaLoadException PathError(string message, Exception? inner = null) =>
        new(message, inner) { PropertyName = nameof(AnimationSource.Path) };
}
