namespace ConfigurationEngine.Media;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ConfigurationEngine.Model;

/// <summary>
/// Az <see cref="IFrameLoader"/> megvalósítása (Wrapper Facade): a WPF képdekódolását és a fájlrendszert rejti el.
/// A képkockákat dekódolja (<see cref="FrameDecoder"/>), és egyenként skálázza a kért méretre, így egyszerre csak
/// egy eredeti méretű képkocka van a memóriában. A munka háttérszálon fut, a képkockák fagyasztottak.
/// </summary>
public sealed class FrameLoader : IFrameLoader
{
    private const int BytesPerPixel = 4;

    private readonly MediaPathResolver _paths;

    public FrameLoader(MediaPathResolver? paths = null)
    {
        _paths = paths ?? new MediaPathResolver();
    }

    public Task<IReadOnlyList<BitmapSource>> LoadFramesAsync(
        AnimationSource source,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        return Task.Run(() => Load(source, width, height, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<BitmapSource> Load(AnimationSource source, int width, int height, CancellationToken cancellationToken)
    {
        var files = MediaFiles.List(source, _paths.Resolve(source.Path));
        var frames = new List<BitmapSource>();
        FrameDecoder.Decode(source, files, frame => frames.Add(Fit(frame, width, height)), cancellationToken);
        return frames;
    }

    /// <summary>
    /// Aránytartó skálázás a <paramref name="width"/> × <paramref name="height"/> területre, alsó-középre igazítva
    /// (a karakter "talpa" a terület alján van), a maradék átlátszó.
    /// </summary>
    internal static BitmapSource Fit(BitmapSource frame, int width, int height)
    {
        var scale = Math.Min((double)width / frame.PixelWidth, (double)height / frame.PixelHeight);
        BitmapSource scaled = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
        if (scaled.Format != PixelFormats.Pbgra32)
        {
            scaled = new FormatConvertedBitmap(scaled, PixelFormats.Pbgra32, null, 0);
        }

        // A skálázott méret kerekítés miatt egy pixellel túlléphet a területen.
        var scaledWidth = Math.Min(scaled.PixelWidth, width);
        var scaledHeight = Math.Min(scaled.PixelHeight, height);
        var pixels = new int[scaledWidth * scaledHeight];
        scaled.CopyPixels(new Int32Rect(0, 0, scaledWidth, scaledHeight), pixels, scaledWidth * BytesPerPixel, 0);

        var result = new int[width * height];
        var left = (width - scaledWidth) / 2;
        var top = height - scaledHeight;
        for (var row = 0; row < scaledHeight; row++)
        {
            Array.Copy(pixels, row * scaledWidth, result, (top + row) * width + left, scaledWidth);
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, result, width * BytesPerPixel);
        bitmap.Freeze();
        return bitmap;
    }
}
