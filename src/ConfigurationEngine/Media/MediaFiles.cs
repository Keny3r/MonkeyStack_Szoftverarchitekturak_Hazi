namespace ConfigurationEngine.Media;

using System.IO;
using ConfigurationEngine.Model;

/// <summary>
/// Egy animációs forrás fájljainak összegyűjtése a gyors ellenőrzésekkel (létezés, kiterjesztés, fájlméret,
/// képszám), dekódolás nélkül. Ez a <see cref="Validation.MediaValidationDepth.Quick"/> ellenőrzés,
/// és a dekódolás előtt is lefut.
/// </summary>
internal static class MediaFiles
{
    /// <summary>
    /// A forrás képfájljai a lejátszás sorrendjében: GIF és sprite sheet esetén maga a fájl,
    /// képsorozat-mappánál a mappa (almappák nélküli) képei természetes sorrendben.
    /// </summary>
    /// <exception cref="MediaLoadException">Ha valamelyik gyors ellenőrzés nem teljesül.</exception>
    public static IReadOnlyList<string> List(AnimationSource source, string fullPath)
    {
        try
        {
            return source.SourceType switch
            {
                AnimationSourceType.Gif => [CheckFile(fullPath, ConfigurationLimits.GifExtensions, "A GIF forrás kiterjesztése .gif kell legyen.")],
                AnimationSourceType.SpriteSheet => [CheckFile(fullPath, ConfigurationLimits.ImageExtensions, $"A sprite sheet kiterjesztése {ImageExtensionList} lehet.")],
                AnimationSourceType.ImageFolder => ListFolder(fullPath),
                _ => throw new MediaLoadException("Ismeretlen forrástípus.") { PropertyName = nameof(AnimationSource.SourceType) }
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw PathError("A fájl vagy mappa nem olvasható.", ex);
        }
    }

    private static string ImageExtensionList => string.Join(", ", ConfigurationLimits.ImageExtensions.Order());

    private static string CheckFile(string fullPath, IReadOnlySet<string> extensions, string extensionMessage)
    {
        if (!File.Exists(fullPath))
        {
            throw PathError(Directory.Exists(fullPath)
                ? "Az útvonal mappára mutat, ehhez a típushoz fájl kell."
                : "A fájl nem található.");
        }

        if (!extensions.Contains(Path.GetExtension(fullPath)))
        {
            throw PathError(extensionMessage);
        }

        CheckSize(fullPath, "A fájl");
        return fullPath;
    }

    private static string[] ListFolder(string fullPath)
    {
        if (!Directory.Exists(fullPath))
        {
            throw PathError(File.Exists(fullPath)
                ? "Az útvonal fájlra mutat, a képsorozathoz mappa kell."
                : "A mappa nem található.");
        }

        var files = Directory.EnumerateFiles(fullPath)
            .Where(f => ConfigurationLimits.ImageExtensions.Contains(Path.GetExtension(f)))
            .Order(NaturalFileNameComparer.Instance)
            .ToArray();

        if (files.Length == 0)
        {
            throw PathError($"A mappában nincs kép ({ImageExtensionList}).");
        }

        if (files.Length > ConfigurationLimits.MediaMaxFrames)
        {
            throw PathError($"A mappában legfeljebb {ConfigurationLimits.MediaMaxFrames} kép lehet, most {files.Length} van.");
        }

        foreach (var file in files)
        {
            CheckSize(file, $"A(z) {Path.GetFileName(file)} kép");
        }

        return files;
    }

    private static void CheckSize(string file, string subject)
    {
        if (new FileInfo(file).Length > ConfigurationLimits.MediaFileMaxBytes)
        {
            throw PathError($"{subject} legfeljebb {ConfigurationLimits.MediaFileMaxBytes / (1024 * 1024)} MB lehet.");
        }
    }

    private static MediaLoadException PathError(string message, Exception? inner = null) =>
        new(message, inner) { PropertyName = nameof(AnimationSource.Path) };
}
