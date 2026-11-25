namespace ConfigurationEngine.Tests;

using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using ConfigurationEngine.Storage;
using ConfigurationEngine.Validation;
using MonkeyStack.Core.Timing;

// A ConfigurationService tesztjeinek hamis függőségei.

internal sealed class FakeStore : IConfigurationStore
{
    public ConfigurationData Data { get; set; } = new();

    public bool Fail { get; set; }

    /// <summary>A következő betöltés jelzi, hogy az adatbázist visszaállította (mint a hiányzó vagy sérült fájlnál).</summary>
    public bool ResetOnNextLoad { get; set; }

    public int Loads => _loads;

    private int _loads;

    public event EventHandler? Changed;

    public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _loads);
        if (Fail)
        {
            return Task.FromException<ConfigurationLoadResult>(new IOException("zárolt adatbázis"));
        }

        bool reset = ResetOnNextLoad;
        ResetOnNextLoad = false;
        return Task.FromResult(new ConfigurationLoadResult(ModelCopies.Copy(Data), [], reset));
    }

    /// <summary>A sikeres mentések száma.</summary>
    public int Saves => _saves;

    private int _saves;

    /// <summary>Elmenti a másolatot, és kiváltja a <see cref="Changed"/> eseményt (az azonosítókat nem osztja ki).</summary>
    public Task SaveAsync(ConfigurationData data, CancellationToken cancellationToken = default)
    {
        Data = ModelCopies.Copy(data);
        Interlocked.Increment(ref _saves);
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Útvonalanként eltérő számú, a kért méretű üres képkockát ad; a <see cref="Failing"/> útvonalakra
/// <see cref="MediaLoadException"/>-t, <see cref="Broken"/> esetén más kivételt dob. A <see cref="Gate"/>
/// nyitásáig vár, így a tesztek a párhuzamos lefutásokat determinisztikusan állíthatják elő.
/// </summary>
internal sealed class FakeFrameLoader : IFrameLoader
{
    private readonly ConcurrentDictionary<string, int> _calls = new();

    public HashSet<string> Failing { get; } = [];

    public bool Broken { get; set; }

    public bool Yield { get; set; }

    public TaskCompletionSource? Gate { get; set; }

    public TaskCompletionSource Started { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>A kapun túljutott betöltések szélességei.</summary>
    public ConcurrentBag<int> Widths { get; } = [];

    /// <summary>Útvonalanként megadott képkockaszám; a többi útvonal 2–6 képkockát kap.</summary>
    public Dictionary<string, int> FrameCounts { get; } = [];

    public int FrameCountOf(string path) =>
        FrameCounts.TryGetValue(path, out int count) ? count : 2 + (Math.Abs(string.GetHashCode(path, StringComparison.Ordinal)) % 5);

    public int CallsFor(string path) => _calls.GetValueOrDefault(path);

    public async Task<IReadOnlyList<BitmapSource>> LoadFramesAsync(
        AnimationSource source,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        Started.TrySetResult();
        if (Yield)
        {
            await Task.Yield();
        }

        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        _calls.AddOrUpdate(source.Path, 1, (_, n) => n + 1);
        Widths.Add(width);
        if (Broken)
        {
            throw new InvalidOperationException("váratlan hiba");
        }

        if (Failing.Contains(source.Path))
        {
            throw new MediaLoadException($"{source.Path} nem található.") { PropertyName = nameof(AnimationSource.Path) };
        }

        var frame = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, new byte[width * height * 4], width * 4);
        frame.Freeze();
        return Enumerable.Repeat(frame, FrameCountOf(source.Path)).ToArray();
    }
}


/// <summary>Kézzel léptethető <see cref="IFrameLoop"/>: a tesztek maguk küldik az ütemeket.</summary>
internal sealed class ManualFrameLoop : IFrameLoop
{
    public event EventHandler<FrameTickEventArgs>? Tick;

    public bool HasSubscribers => Tick is not null;

    public void Step(TimeSpan deltaTime) => Tick?.Invoke(this, new FrameTickEventArgs(deltaTime));
}

/// <summary>
/// Fájlrendszer nélküli <see cref="IMediaValidator"/>: a <see cref="Rejected"/> útvonalú forrásokra hibát ad, a
/// képméretet a <see cref="ImageSizes"/>-ből adja.
/// </summary>
internal sealed class FakeMediaValidator : IMediaValidator
{
    public HashSet<string> Rejected { get; } = [];

    public Dictionary<string, PixelSize> ImageSizes { get; } = [];

    /// <summary>A kapott mélységek, sorban.</summary>
    public ConcurrentQueue<MediaValidationDepth> Depths { get; } = new();

    public Task<IReadOnlyList<ValidationError>> ValidateAsync(
        AnimationSource source,
        MediaValidationDepth depth,
        CancellationToken cancellationToken = default)
    {
        Depths.Enqueue(depth);
        IReadOnlyList<ValidationError> errors = Rejected.Contains(source.Path)
            ? [new ValidationError(ConfigurationItemKind.AnimationSource, source.Id.ToString(), nameof(AnimationSource.Path), $"{source.Path} nem található.")]
            : [];
        return Task.FromResult(errors);
    }

    public Task<PixelSize> ReadImageSizeAsync(AnimationSource source, CancellationToken cancellationToken = default) =>
        ImageSizes.TryGetValue(source.Path, out var size)
            ? Task.FromResult(size)
            : Task.FromException<PixelSize>(new MediaLoadException($"{source.Path} nem található."));
}
