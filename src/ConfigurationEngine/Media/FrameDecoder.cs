namespace ConfigurationEngine.Media;

using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ConfigurationEngine.Model;

/// <summary>
/// Egy animációs forrás képkockáinak dekódolása eredeti méretben, <see cref="PixelFormats.Pbgra32"/> formátumban.
/// A korlátokat (képméret, képkockaszám) a teljes dekódolás előtt, a fejlécből ellenőrzi, és csak a beépített
/// PNG, JPEG, BMP és GIF dekódert fogadja el, így a rendszerre telepített egyéb képkodekek nem futnak.
/// Csak pixelpufferekkel dolgozik, ezért bármelyik szálon hívható.
/// </summary>
internal static class FrameDecoder
{
    private const int BytesPerPixel = 4;

    // A GIF képkocka eltávolítási módja (Graphic Control Extension, "Disposal Method").
    private const int DisposalRestoreBackground = 2;
    private const int DisposalRestorePrevious = 3;

    /// <summary>
    /// Dekódolja a forrás képkockáit, és sorban átadja őket az <paramref name="onFrame"/>-nek. A képkockákat nem
    /// gyűjti össze, így egyszerre csak egy eredeti méretű képkocka van a memóriában.
    /// </summary>
    /// <param name="files">A forrás fájljai (<see cref="MediaFiles.List"/>).</param>
    /// <exception cref="MediaLoadException">Ha a forrás nem dekódolható vagy túllépi a korlátokat.</exception>
    public static void Decode(
        AnimationSource source,
        IReadOnlyList<string> files,
        Action<BitmapSource> onFrame,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (source.SourceType)
            {
                case AnimationSourceType.Gif:
                    DecodeGif(files[0], onFrame, cancellationToken);
                    break;
                case AnimationSourceType.SpriteSheet:
                    DecodeSpriteSheet(source, files[0], onFrame, cancellationToken);
                    break;
                case AnimationSourceType.ImageFolder:
                    DecodeFolder(files, onFrame, cancellationToken);
                    break;
                default:
                    throw new MediaLoadException("Ismeretlen forrástípus.") { PropertyName = nameof(AnimationSource.SourceType) };
            }
        }
        catch (Exception ex) when (ex is not (MediaLoadException or OperationCanceledException or OutOfMemoryException))
        {
            // A WPF dekóderei sérült fájlnál sokféle kivételt dobnak (FileFormatException, NotSupportedException, COMException…).
            throw PathError("A kép nem dekódolható, a fájl sérült vagy nem támogatott formátumú.", ex);
        }
    }

    private static void DecodeGif(string file, Action<BitmapSource> onFrame, CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(file);
        var decoder = OpenDecoder(file);
        if (decoder is not GifBitmapDecoder)
        {
            throw PathError($"A(z) {name} nem GIF fájl.");
        }

        CheckFrameCount(decoder.Frames.Count, name);

        // A logikai képernyő a GIF teljes mérete; a képkockák ennek egy-egy téglalapját rajzolják újra.
        var first = decoder.Frames[0];
        var width = ReadMetadata(() => decoder.Metadata, "/logscrdesc/Width", first.PixelWidth);
        var height = ReadMetadata(() => decoder.Metadata, "/logscrdesc/Height", first.PixelHeight);
        CheckImageSize(width, height, name);

        var canvas = new int[width * height];
        int[]? saved = null;
        var previousDisposal = 0;
        var previousRect = Int32Rect.Empty;

        foreach (var frame in decoder.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckImageSize(frame.PixelWidth, frame.PixelHeight, name);

            // Az előző képkocka eltávolítása, mielőtt az új rákerül.
            if (previousDisposal == DisposalRestoreBackground)
            {
                Clear(canvas, width, previousRect);
            }
            else if (previousDisposal == DisposalRestorePrevious && saved is not null)
            {
                saved.CopyTo(canvas, 0);
            }

            var rect = new Int32Rect(
                ReadMetadata(() => frame.Metadata, "/imgdesc/Left", 0),
                ReadMetadata(() => frame.Metadata, "/imgdesc/Top", 0),
                frame.PixelWidth,
                frame.PixelHeight);
            var disposal = ReadMetadata(() => frame.Metadata, "/grctlext/Disposal", 0);

            if (disposal == DisposalRestorePrevious)
            {
                saved ??= new int[canvas.Length];
                canvas.CopyTo(saved, 0);
            }

            DrawOver(canvas, width, height, ToPixels(frame), rect);
            onFrame(Create(canvas, width, height));

            previousDisposal = disposal;
            previousRect = rect;
        }
    }

    private static void DecodeSpriteSheet(
        AnimationSource source,
        string file,
        Action<BitmapSource> onFrame,
        CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(file);
        var sheet = OpenImage(file);
        CheckImageSize(sheet.PixelWidth, sheet.PixelHeight, name);

        // A rács mezőit a ConfigurationValidator már ellenőrizte (kitöltöttek, tartományon belül).
        int frameWidth = source.FrameWidth!.Value, frameHeight = source.FrameHeight!.Value;
        int columns = source.Columns!.Value, rows = source.Rows!.Value, frameCount = source.FrameCount!.Value;

        if (sheet.PixelWidth != columns * frameWidth || sheet.PixelHeight != rows * frameHeight)
        {
            throw new MediaLoadException(
                $"A kép mérete {sheet.PixelWidth} × {sheet.PixelHeight} px, a rács alapján " +
                $"{columns * frameWidth} × {rows * frameHeight} px kellene ({columns} × {frameWidth}, {rows} × {frameHeight}).");
        }

        if (frameCount > ConfigurationLimits.MediaMaxFrames)
        {
            throw new MediaLoadException($"Legfeljebb {ConfigurationLimits.MediaMaxFrames} képkocka lehet.")
            {
                PropertyName = nameof(AnimationSource.FrameCount)
            };
        }

        var pixels = ToPixels(sheet);
        for (var index = 0; index < frameCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rect = new Int32Rect(index % columns * frameWidth, index / columns * frameHeight, frameWidth, frameHeight);
            onFrame(Create(Crop(pixels, sheet.PixelWidth, rect), frameWidth, frameHeight));
        }
    }

    private static void DecodeFolder(IReadOnlyList<string> files, Action<BitmapSource> onFrame, CancellationToken cancellationToken)
    {
        int width = 0, height = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            var image = OpenImage(file);
            CheckImageSize(image.PixelWidth, image.PixelHeight, name);

            if (width == 0)
            {
                (width, height) = (image.PixelWidth, image.PixelHeight);
            }
            else if (image.PixelWidth != width || image.PixelHeight != height)
            {
                throw PathError(
                    $"A mappa képei nem egyforma méretűek: {Path.GetFileName(files[0])} {width} × {height} px, " +
                    $"{name} {image.PixelWidth} × {image.PixelHeight} px.");
            }

            onFrame(Create(ToPixels(image), width, height));
        }
    }

    /// <summary>
    /// Megnyitja a fájlt, és a tartalma alapján dekódert választ. A fájlt egyben beolvassa és rögtön elengedi,
    /// így az nem marad zárolva. A pixelek csak a képkockák tényleges kiolvasásakor dekódolódnak.
    /// </summary>
    private static BitmapDecoder OpenDecoder(string file)
    {
        byte[] bytes;
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            // A gyors ellenőrzés óta a fájl megnőhetett.
            if (stream.Length > ConfigurationLimits.MediaFileMaxBytes)
            {
                throw PathError($"A(z) {Path.GetFileName(file)} legfeljebb {ConfigurationLimits.MediaFileMaxBytes / (1024 * 1024)} MB lehet.");
            }

            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }

        var decoder = BitmapDecoder.Create(new MemoryStream(bytes, writable: false), BitmapCreateOptions.None, BitmapCacheOption.None);
        if (decoder is not (PngBitmapDecoder or JpegBitmapDecoder or BmpBitmapDecoder or GifBitmapDecoder) || decoder.Frames.Count == 0)
        {
            throw PathError($"A(z) {Path.GetFileName(file)} nem PNG, JPEG, BMP vagy GIF kép.");
        }

        return decoder;
    }

    /// <summary>Egyképes forrás (sprite sheet, a mappa egy képe): a fájl első képkockája.</summary>
    private static BitmapFrame OpenImage(string file)
    {
        var decoder = OpenDecoder(file);
        if (decoder is GifBitmapDecoder)
        {
            throw PathError($"A(z) {Path.GetFileName(file)} GIF fájl; GIF-hez a GIF forrástípust válaszd.");
        }

        return decoder.Frames[0];
    }

    private static void CheckImageSize(int width, int height, string name)
    {
        var max = ConfigurationLimits.MediaImageMaxPx;
        if (width <= 0 || height <= 0 || width > max || height > max)
        {
            throw PathError($"A(z) {name} mérete {width} × {height} px; legfeljebb {max} × {max} px lehet.");
        }
    }

    private static void CheckFrameCount(int count, string name)
    {
        if (count > ConfigurationLimits.MediaMaxFrames)
        {
            throw PathError($"A(z) {name} {count} képkockából áll; legfeljebb {ConfigurationLimits.MediaMaxFrames} lehet.");
        }
    }

    /// <summary>Egy GIF metaadat-mező értéke; ha hiányzik vagy nem olvasható, a <paramref name="fallback"/>.</summary>
    private static int ReadMetadata(Func<ImageMetadata?> metadata, string query, int fallback)
    {
        try
        {
            return metadata() is BitmapMetadata bitmapMetadata && bitmapMetadata.GetQuery(query) is { } value
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
                : fallback;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException or COMException)
        {
            return fallback;
        }
    }

    private static int[] ToPixels(BitmapSource image)
    {
        var converted = image.Format == PixelFormats.Pbgra32 ? image : new FormatConvertedBitmap(image, PixelFormats.Pbgra32, null, 0);
        var pixels = new int[converted.PixelWidth * converted.PixelHeight];
        converted.CopyPixels(pixels, converted.PixelWidth * BytesPerPixel, 0);
        return pixels;
    }

    /// <summary>A képkockát a vászonra rajzolja; a GIF átlátszó pixelei alatt a vászon megmarad.</summary>
    private static void DrawOver(int[] canvas, int canvasWidth, int canvasHeight, int[] frame, Int32Rect rect)
    {
        var clipped = Clip(rect, canvasWidth, canvasHeight);
        for (var y = clipped.Y; y < clipped.Y + clipped.Height; y++)
        {
            for (var x = clipped.X; x < clipped.X + clipped.Width; x++)
            {
                var pixel = frame[(y - rect.Y) * rect.Width + (x - rect.X)];
                if ((pixel >>> 24) != 0)
                {
                    canvas[y * canvasWidth + x] = pixel;
                }
            }
        }
    }

    private static void Clear(int[] canvas, int canvasWidth, Int32Rect rect)
    {
        var clipped = Clip(rect, canvasWidth, canvas.Length / canvasWidth);
        for (var y = clipped.Y; y < clipped.Y + clipped.Height; y++)
        {
            Array.Clear(canvas, y * canvasWidth + clipped.X, clipped.Width);
        }
    }

    private static Int32Rect Clip(Int32Rect rect, int width, int height)
    {
        int left = Math.Max(rect.X, 0), top = Math.Max(rect.Y, 0);
        int right = Math.Min(rect.X + rect.Width, width), bottom = Math.Min(rect.Y + rect.Height, height);
        return right > left && bottom > top ? new Int32Rect(left, top, right - left, bottom - top) : new Int32Rect(0, 0, 0, 0);
    }

    private static int[] Crop(int[] pixels, int width, Int32Rect rect)
    {
        var result = new int[rect.Width * rect.Height];
        for (var row = 0; row < rect.Height; row++)
        {
            Array.Copy(pixels, (rect.Y + row) * width + rect.X, result, row * rect.Width, rect.Width);
        }

        return result;
    }

    private static BitmapSource Create(int[] pixels, int width, int height)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * BytesPerPixel);
        bitmap.Freeze();
        return bitmap;
    }

    private static MediaLoadException PathError(string message, Exception? inner = null) =>
        new(message, inner) { PropertyName = nameof(AnimationSource.Path) };
}
