namespace ConfigurationEngine.Validation;

using System.Globalization;
using ConfigurationEngine.Media;
using ConfigurationEngine.Model;

/// <summary>
/// Az <see cref="IMediaValidator"/> megvalósítása (schema.md, "Médiafájlok validálása").
/// A <see cref="MediaValidationDepth.Full"/> ellenőrzés ugyanazzal a dekóderrel dolgozik, mint a
/// <see cref="FrameLoader"/>, így amit az ellenőrző elfogad, azt a betöltő is be tudja tölteni.
/// </summary>
public sealed class MediaValidator : IMediaValidator
{
    private readonly MediaPathResolver _paths;

    public MediaValidator(MediaPathResolver? paths = null)
    {
        _paths = paths ?? new MediaPathResolver();
    }

    public Task<IReadOnlyList<ValidationError>> ValidateAsync(
        AnimationSource source,
        MediaValidationDepth depth,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        // A gyors ellenőrzés csak fájlrendszer-lekérdezés, a dekódolás viszont lassú lehet, ezért háttérszálon fut.
        return depth == MediaValidationDepth.Quick
            ? Task.FromResult(Validate(source, depth, cancellationToken))
            : Task.Run(() => Validate(source, depth, cancellationToken), cancellationToken);
    }

    public Task<PixelSize> ReadImageSizeAsync(AnimationSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        // A fájl beolvasása és a fejléc értelmezése lassú lehet (hálózati meghajtó, nagy fájl), ezért háttérszálon fut.
        return Task.Run(() => FrameDecoder.ReadImageSize(source, MediaFiles.List(source, _paths.Resolve(source.Path))), cancellationToken);
    }

    private IReadOnlyList<ValidationError> Validate(AnimationSource source, MediaValidationDepth depth, CancellationToken cancellationToken)
    {
        try
        {
            var files = MediaFiles.List(source, _paths.Resolve(source.Path));
            if (depth == MediaValidationDepth.Full)
            {
                FrameDecoder.Decode(source, files, onFrame: null, cancellationToken);
            }

            return [];
        }
        catch (MediaLoadException ex)
        {
            return
            [
                new ValidationError(
                    ConfigurationItemKind.AnimationSource,
                    source.Id.ToString(CultureInfo.InvariantCulture),
                    ex.PropertyName,
                    ex.Message)
            ];
        }
    }
}
