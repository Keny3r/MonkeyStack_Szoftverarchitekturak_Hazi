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

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(AssetsDirectory, path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw PathError("Az útvonal érvénytelen.", ex);
        }

        // A hálózati meghajtóra csatolt megosztás (pl. Z:\) is hálózati útvonal; egy elérhetetlen megosztás
        // a fájlrendszer-hívásokat is megakaszthatná. A szállított képek (relatív útvonal) helyét nem ellenőrizzük.
        if (Path.IsPathRooted(path) && IsNetworkDrive(fullPath))
        {
            throw PathError("Hálózati meghajtón lévő fájl nem megengedett.");
        }

        return fullPath;
    }

    private static bool IsNetworkDrive(string fullPath)
    {
        try
        {
            var root = Path.GetPathRoot(fullPath);
            return !string.IsNullOrEmpty(root) && new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsSeparator(char c) => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

    private static MediaLoadException PathError(string message, Exception? inner = null) =>
        new(message, inner) { PropertyName = nameof(AnimationSource.Path) };
}
