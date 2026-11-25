namespace ConfigurationEngine.Tests.Media;

using System.IO;
using System.Windows.Media;
using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using static ConfigurationEngine.Tests.Media.TestImages;

public sealed class FrameLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MonkeyStackTests", Guid.NewGuid().ToString("N"));
    private readonly FrameLoader _loader;

    public FrameLoaderTests()
    {
        Directory.CreateDirectory(_directory);
        _loader = new FrameLoader(new MediaPathResolver(_directory));
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

    [Fact]
    public async Task WideImage_IsScaledProportionallyToTheBottom()
    {
        WritePng(Path.Combine(_directory, "wide", "1.png"), 20, 10, Red);

        var frames = await _loader.LoadFramesAsync(Folder("wide"), 40, 40);

        var frame = Assert.Single(frames);
        Assert.True(frame.IsFrozen);
        Assert.Equal((40, 40), (frame.PixelWidth, frame.PixelHeight));
        Assert.Equal(0, PixelAt(frame, 20, 5).A);   // felül átlátszó
        Assert.Equal(Red, PixelAt(frame, 20, 30));  // alul a kép, 40 × 20 px
    }

    [Fact]
    public async Task TallImage_IsCentredHorizontally()
    {
        WritePng(Path.Combine(_directory, "tall", "1.png"), 10, 20, Red);

        var frame = Assert.Single(await _loader.LoadFramesAsync(Folder("tall"), 40, 40));

        Assert.Equal(0, PixelAt(frame, 5, 20).A);   // a 20 px széles kép a 10–29. oszlopban van
        Assert.Equal(Red, PixelAt(frame, 20, 20));
        Assert.Equal(0, PixelAt(frame, 35, 20).A);
    }

    [Fact]
    public async Task ImageFolder_IsReadInNaturalOrder()
    {
        WritePng(Path.Combine(_directory, "seq", "frame10.png"), 4, 4, Blue);
        WritePng(Path.Combine(_directory, "seq", "frame2.png"), 4, 4, Green);
        WritePng(Path.Combine(_directory, "seq", "frame1.png"), 4, 4, Red);
        File.WriteAllText(Path.Combine(_directory, "seq", "notes.txt"), "nem kép");

        var frames = await _loader.LoadFramesAsync(Folder("seq"), 4, 4);

        Assert.Equal([Red, Green, Blue], frames.Select(f => PixelAt(f, 2, 2)));
    }

    [Fact]
    public async Task SpriteSheet_IsCutLeftToRightThenTopToBottom()
    {
        // 3 × 2 rács, 5 képkocka: piros, zöld, kék / piros, zöld, (üres).
        Color[] colors = [Red, Green, Blue];
        WritePng(Path.Combine(_directory, "sheet.png"), 30, 20, (x, _) => colors[x / 10]);
        var source = new AnimationSource
        {
            Name = "Sheet",
            SourceType = AnimationSourceType.SpriteSheet,
            Path = "sheet.png",
            FrameWidth = 10,
            FrameHeight = 10,
            Columns = 3,
            Rows = 2,
            FrameCount = 5
        };

        var frames = await _loader.LoadFramesAsync(source, 10, 10);

        Assert.Equal([Red, Green, Blue, Red, Green], frames.Select(f => PixelAt(f, 5, 5)));
    }

    [Fact]
    public async Task Gif_FramesAreComposedWithDisposalMethods()
    {
        // 4 × 4 logikai képernyő.
        // 1. teljes piros, megmarad (1); 2. zöld 2 × 2 a (2, 2) helyen, utána az előző állapot jön vissza (3);
        // 3. kék pixel a (0, 0) helyen, utána átlátszóra törlődik (2); 4. zöld pixel az (1, 1) helyen.
        WriteGif(
            Path.Combine(_directory, "anim.gif"), 4, 4,
            new GifFrame(0, 0, 4, 4, Enumerable.Repeat((byte)1, 16).ToArray(), Disposal: 1),
            new GifFrame(2, 2, 2, 2, [2, 2, 2, 2], Disposal: 3),
            new GifFrame(0, 0, 1, 1, [3], Disposal: 2),
            new GifFrame(1, 1, 1, 1, [2], Disposal: 0));
        var source = new AnimationSource { Name = "Gif", SourceType = AnimationSourceType.Gif, Path = "anim.gif" };

        var frames = await _loader.LoadFramesAsync(source, 4, 4);

        Assert.Equal(4, frames.Count);
        Assert.Equal(Red, PixelAt(frames[0], 3, 3));
        Assert.Equal(Green, PixelAt(frames[1], 3, 3));
        Assert.Equal(Red, PixelAt(frames[1], 0, 0));
        Assert.Equal(Red, PixelAt(frames[2], 3, 3));       // a 2. képkocka előtti állapot jött vissza
        Assert.Equal(Blue, PixelAt(frames[2], 0, 0));
        Assert.Equal(0, PixelAt(frames[3], 0, 0).A);       // a kék pixel helye átlátszóra törlődött
        Assert.Equal(Green, PixelAt(frames[3], 1, 1));
        Assert.Equal(Red, PixelAt(frames[3], 2, 2));
    }

    [Fact]
    public async Task UndecodableFile_ThrowsMediaLoadExceptionForPath()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "broken"));
        File.WriteAllText(Path.Combine(_directory, "broken", "1.png"), "nem kép");

        var ex = await Assert.ThrowsAsync<MediaLoadException>(() => _loader.LoadFramesAsync(Folder("broken"), 10, 10));

        Assert.Equal(nameof(AnimationSource.Path), ex.PropertyName);
    }

    [Fact]
    public async Task LoadedFramesAreLimitedByCharacterSize()
    {
        // 65 képkocka: 100 × 100 px-en belefér a keretbe, 512 × 512 px-en (64 MB / 1 MB = 64) nem.
        var frames = Enumerable.Range(0, 65).Select(_ => new GifFrame(0, 0, 1, 1, [1], Disposal: 1)).ToArray();
        WriteGif(Path.Combine(_directory, "many.gif"), 1, 1, frames);
        var source = new AnimationSource { Name = "Gif", SourceType = AnimationSourceType.Gif, Path = "many.gif" };

        Assert.Equal(65, (await _loader.LoadFramesAsync(source, 100, 100)).Count);
        await Assert.ThrowsAsync<MediaLoadException>(() => _loader.LoadFramesAsync(source, 512, 512));
    }

    [Fact]
    public async Task LoadedFile_IsNotLocked()
    {
        var file = Path.Combine(_directory, "locked", "1.png");
        WritePng(file, 4, 4, Red);

        await _loader.LoadFramesAsync(Folder("locked"), 4, 4);

        File.Delete(file);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task ShippedAnimations_AreLoadable()
    {
        var loader = new FrameLoader();

        foreach (var source in ConfigurationDefaults.CreateMovementSources().Concat(ConfigurationDefaults.CreateOverlaySources()))
        {
            var frames = await loader.LoadFramesAsync(source, 100, 100);
            Assert.NotEmpty(frames);
        }
    }

    private static AnimationSource Folder(string path) =>
        new() { Name = path, SourceType = AnimationSourceType.ImageFolder, Path = path };
}
