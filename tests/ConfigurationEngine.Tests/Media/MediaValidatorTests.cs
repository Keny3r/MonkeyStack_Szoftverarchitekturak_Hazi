namespace ConfigurationEngine.Tests.Media;

using System.IO;
using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;
using static ConfigurationEngine.Tests.Media.TestImages;

public sealed class MediaValidatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MonkeyStackTests", Guid.NewGuid().ToString("N"));
    private readonly MediaValidator _validator;

    public MediaValidatorTests()
    {
        Directory.CreateDirectory(_directory);
        _validator = new MediaValidator(new MediaPathResolver(_directory));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Theory]
    [InlineData(@"\\szerver\megosztas\majom.gif")]
    [InlineData("//szerver/megosztas/majom.gif")]
    [InlineData(@"\\?\C:\majom.gif")]
    [InlineData(@"C:majom.gif")]
    public async Task NetworkDeviceOrDriveRelativePath_IsRejected(string path)
    {
        var error = Assert.Single(await _validator.ValidateAsync(Gif(path), MediaValidationDepth.Quick));

        Assert.Equal(nameof(AnimationSource.Path), error.PropertyName);
    }

    [Fact]
    public async Task RelativePath_IsResolvedAgainstAssets()
    {
        WriteGif(Path.Combine(_directory, "majom.gif"), 1, 1, new GifFrame(0, 0, 1, 1, [1], 0));

        Assert.Empty(await _validator.ValidateAsync(Gif("majom.gif"), MediaValidationDepth.Full));
        Assert.Empty(await _validator.ValidateAsync(Gif(Path.Combine(_directory, "majom.gif")), MediaValidationDepth.Full));
    }

    [Fact]
    public async Task MissingFile_IsRejected()
    {
        var error = Assert.Single(await _validator.ValidateAsync(Gif("nincs.gif"), MediaValidationDepth.Quick));

        Assert.Equal(nameof(AnimationSource.Path), error.PropertyName);
        Assert.Equal("7", error.ItemKey);
    }

    [Fact]
    public async Task WrongExtension_IsRejected()
    {
        WritePng(Path.Combine(_directory, "majom.png"), 1, 1, Red);

        Assert.Single(await _validator.ValidateAsync(Gif("majom.png"), MediaValidationDepth.Quick));
    }

    [Fact]
    public async Task ContentIsOnlyCheckedInFullDepth()
    {
        File.WriteAllText(Path.Combine(_directory, "hamis.gif"), "nem GIF");

        Assert.Empty(await _validator.ValidateAsync(Gif("hamis.gif"), MediaValidationDepth.Quick));
        Assert.Single(await _validator.ValidateAsync(Gif("hamis.gif"), MediaValidationDepth.Full));
    }

    [Fact]
    public async Task PngWithGifExtension_IsRejected()
    {
        WritePng(Path.Combine(_directory, "valojaban-png.gif"), 1, 1, Red);

        Assert.Single(await _validator.ValidateAsync(Gif("valojaban-png.gif"), MediaValidationDepth.Full));
    }

    [Fact]
    public async Task OtherImageFormatWithPngExtension_IsRejected()
    {
        // TIFF tartalom .png kiterjesztéssel: a TIFF dekóder el sem indulhat.
        Directory.CreateDirectory(Path.Combine(_directory, "tiff"));
        var encoder = new System.Windows.Media.Imaging.TiffBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(
            System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4)));
        using (var stream = File.Create(Path.Combine(_directory, "tiff", "1.png")))
        {
            encoder.Save(stream);
        }

        var error = Assert.Single(await _validator.ValidateAsync(Folder("tiff"), MediaValidationDepth.Full));

        Assert.Equal(nameof(AnimationSource.Path), error.PropertyName);
    }

    [Fact]
    public async Task GifWithTooManyFrames_IsRejectedBeforeDecoding()
    {
        var frames = Enumerable.Range(0, ConfigurationLimits.MediaMaxFrames + 1)
            .Select(_ => new GifFrame(0, 0, 1, 1, [1], Disposal: 1)).ToArray();
        WriteGif(Path.Combine(_directory, "sok.gif"), 1, 1, frames);

        Assert.Equal(ConfigurationLimits.MediaMaxFrames + 1, FrameDecoder.ReadGifLayout(File.ReadAllBytes(Path.Combine(_directory, "sok.gif")), int.MaxValue).FrameCount);
        var error = Assert.Single(await _validator.ValidateAsync(Gif("sok.gif"), MediaValidationDepth.Full));
        Assert.Contains("képkockából áll", error.Message);
    }

    [Fact]
    public async Task GifWithTooManyDecodedPixels_IsRejectedBeforeDecoding()
    {
        // 8 képkocka egy 4096 × 4096 px-es vásznon: a fájl apró, de 134 millió pixelt kellene dekódolni.
        var frames = Enumerable.Range(0, 8).Select(_ => new GifFrame(0, 0, 1, 1, [1], Disposal: 1)).ToArray();
        WriteGif(Path.Combine(_directory, "nagy-vaszon.gif"), 4096, 4096, frames);

        var error = Assert.Single(await _validator.ValidateAsync(Gif("nagy-vaszon.gif"), MediaValidationDepth.Full));

        Assert.Contains("millió pixel", error.Message);
    }

    [Fact]
    public async Task GifWithEmptyLogicalScreen_UsesFrameExtent()
    {
        WriteGif(Path.Combine(_directory, "ures-vaszon.gif"), 0, 0, new GifFrame(1, 1, 2, 2, [1, 1, 1, 1], Disposal: 0));

        Assert.Empty(await _validator.ValidateAsync(Gif("ures-vaszon.gif"), MediaValidationDepth.Full));
        Assert.Equal(
            new FrameDecoder.GifLayout(1, 3, 3),
            FrameDecoder.ReadGifLayout(File.ReadAllBytes(Path.Combine(_directory, "ures-vaszon.gif")), int.MaxValue));
    }

    [Fact]
    public void FrameSinkFailure_IsNotReportedAsCorruptFile()
    {
        WriteGif(Path.Combine(_directory, "jo.gif"), 1, 1, new GifFrame(0, 0, 1, 1, [1], Disposal: 0));
        var source = Gif("jo.gif");
        var files = MediaFiles.List(source, Path.Combine(_directory, "jo.gif"));

        Assert.Throws<InvalidOperationException>(() =>
            FrameDecoder.Decode(source, files, _ => throw new InvalidOperationException("hiba a fogadóban"), CancellationToken.None));
    }

    [Fact]
    public async Task SpriteSheetWithMoreFramesThanCells_IsRejected()
    {
        WritePng(Path.Combine(_directory, "sheet.png"), 20, 10, Red);
        var source = new AnimationSource
        {
            Id = 7,
            Name = "Sheet",
            SourceType = AnimationSourceType.SpriteSheet,
            Path = "sheet.png",
            FrameWidth = 10,
            FrameHeight = 10,
            Columns = 2,
            Rows = 1,
            FrameCount = 3
        };

        var error = Assert.Single(await _validator.ValidateAsync(source, MediaValidationDepth.Full));

        Assert.Equal(nameof(AnimationSource.FrameCount), error.PropertyName);
    }

    [Fact]
    public async Task SpriteSheetWithMissingGridFields_IsRejected()
    {
        WritePng(Path.Combine(_directory, "sheet.png"), 10, 10, Red);
        var source = new AnimationSource { Id = 7, Name = "Sheet", SourceType = AnimationSourceType.SpriteSheet, Path = "sheet.png" };

        var error = Assert.Single(await _validator.ValidateAsync(source, MediaValidationDepth.Full));

        Assert.Contains("rács", error.Message);
    }

    [Fact]
    public async Task TooLargeFile_IsRejected()
    {
        using (var stream = File.Create(Path.Combine(_directory, "nagy.gif")))
        {
            stream.SetLength(ConfigurationLimits.MediaFileMaxBytes + 1);
        }

        Assert.Single(await _validator.ValidateAsync(Gif("nagy.gif"), MediaValidationDepth.Quick));
    }

    [Fact]
    public async Task TooLargeImage_IsRejected()
    {
        WritePng(Path.Combine(_directory, "szeles", "1.png"), ConfigurationLimits.MediaImageMaxPx + 1, 1, Red);

        Assert.Single(await _validator.ValidateAsync(Folder("szeles"), MediaValidationDepth.Full));
    }

    [Fact]
    public async Task EmptyFolder_IsRejected()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "ures"));

        Assert.Single(await _validator.ValidateAsync(Folder("ures"), MediaValidationDepth.Quick));
    }

    [Fact]
    public async Task FolderWithDifferentImageSizes_IsRejected()
    {
        WritePng(Path.Combine(_directory, "vegyes", "1.png"), 4, 4, Red);
        WritePng(Path.Combine(_directory, "vegyes", "2.png"), 5, 4, Red);

        Assert.Empty(await _validator.ValidateAsync(Folder("vegyes"), MediaValidationDepth.Quick));
        Assert.Single(await _validator.ValidateAsync(Folder("vegyes"), MediaValidationDepth.Full));
    }

    [Fact]
    public async Task SpriteSheetNotMatchingGrid_IsRejectedForWholeSource()
    {
        WritePng(Path.Combine(_directory, "sheet.png"), 30, 20, Red);
        var source = new AnimationSource
        {
            Id = 7,
            Name = "Sheet",
            SourceType = AnimationSourceType.SpriteSheet,
            Path = "sheet.png",
            FrameWidth = 10,
            FrameHeight = 10,
            Columns = 4,
            Rows = 2,
            FrameCount = 8
        };

        var error = Assert.Single(await _validator.ValidateAsync(source, MediaValidationDepth.Full));

        Assert.Null(error.PropertyName);
    }

    [Fact]
    public async Task ShippedMovementAnimations_PassFullValidation()
    {
        var validator = new MediaValidator();

        foreach (var source in ConfigurationDefaults.CreateMovementSources())
        {
            Assert.Empty(await validator.ValidateAsync(source, MediaValidationDepth.Full));
        }
    }

    private static AnimationSource Gif(string path) =>
        new() { Id = 7, Name = "Gif", SourceType = AnimationSourceType.Gif, Path = path };

    private static AnimationSource Folder(string path) =>
        new() { Id = 7, Name = path, SourceType = AnimationSourceType.ImageFolder, Path = path };
}
