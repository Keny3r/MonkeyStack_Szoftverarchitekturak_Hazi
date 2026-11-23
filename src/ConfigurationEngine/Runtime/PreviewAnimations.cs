namespace ConfigurationEngine.Runtime;

using System.Diagnostics;
using System.Windows.Media.Imaging;
using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using ConfigurationEngine.Storage;
using MonkeyStack.Core.Animation;

/// <summary>
/// A konfigurációs ablak előnézeti animációi (view.md, "Élő előnézet"): a futó konfigurációtól külön példányok, ezért
/// az újratöltés nem érinti őket, csak az eldobás. Ugyanannak a forrásnak a képkockái méretenként megosztottak, így egy
/// új előnézet (pl. sebességváltáskor, a régi eldobása után) nem tölti be újra a forrást. A már egyetlen előnézet által
/// sem használt és nem várt képkockák a következő sikeres előnézet-létrehozáskor dobódnak el. Szálbiztos (Monitor
/// Object, Scoped Locking).
/// </summary>
internal sealed class PreviewAnimations : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly IFrameLoader _frameLoader;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<IMutableAnimation, Entry> _entries = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(string Media, int Width, int Height), Task<IReadOnlyList<BitmapSource>>> _frames = [];
    private readonly Dictionary<(string Media, int Width, int Height), int> _waiting = [];
    private int _width;
    private int _height;
    private bool _disposed;

    public PreviewAnimations(IFrameLoader frameLoader, int width, int height)
    {
        _frameLoader = frameLoader;
        _width = width;
        _height = height;
    }

    /// <summary>A kiadott, még el nem dobott előnézetek száma.</summary>
    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Új előnézet a forrás pillanatképéből, az aktuális méretben. Ha a betöltés közben a méret megváltozott, az
    /// előnézet azonnal az új méretet kapja, a képkockái pedig a háttérben töltődnek be újra.
    /// </summary>
    /// <exception cref="MediaLoadException">Ha a forrás nem tölthető be.</exception>
    /// <exception cref="ObjectDisposedException">Ha a szolgáltatást közben eldobták.</exception>
    public async Task<IAnimation> CreateAsync(AnimationSource source, double framesPerSecond, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var snapshot = source.Copy();
        var media = MediaKey(snapshot);
        int width;
        int height;
        Task<IReadOnlyList<BitmapSource>> loading;
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            (width, height) = (_width, _height);
            loading = Frames(snapshot, media, width, height);
            _waiting[(media, width, height)] = _waiting.GetValueOrDefault((media, width, height)) + 1;
        }

        IReadOnlyList<BitmapSource> frames;
        try
        {
            // A megosztott betöltést egy hívó megszakítása nem állíthatja le, csak a saját várakozását.
            frames = await loading.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_syncRoot)
            {
                StopWaiting((media, width, height));
            }
        }

        bool stale;
        Animation preview;
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            preview = new Animation(frames, width, height, framesPerSecond);
            _entries[preview] = new Entry(snapshot, media) { FramesSize = (width, height) };
            DropUnusedFrames();
            stale = (width, height) != (_width, _height);
            if (stale)
            {
                preview.SetSize(_width, _height);
            }
        }

        if (stale)
        {
            _ = Task.Run(RescaleAsync, CancellationToken.None);
        }

        return preview;
    }

    /// <summary>
    /// Eldobja az előnézetet; ezután nem léptetődik. A képkockái a következő előnézetig megmaradnak, így ugyanannak a
    /// forrásnak az azonnali újrakérése nem tölti be újra. Az ismeretlen vagy már eldobott előnézetet figyelmen kívül hagyja.
    /// </summary>
    public void Release(IAnimation preview)
    {
        lock (_syncRoot)
        {
            if (preview is IMutableAnimation animation)
            {
                _entries.Remove(animation);
            }
        }
    }

    /// <summary>Eldobja az összes előnézetet és a képkockáikat.</summary>
    public void ReleaseAll()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
            _frames.Clear();
        }
    }

    /// <summary>Lépteti az előnézeteket. A zár alatt fut, így egy eldobott előnézet a visszatérés után már nem lép.</summary>
    public void Update(TimeSpan deltaTime)
    {
        lock (_syncRoot)
        {
            foreach (var preview in _entries.Keys)
            {
                preview.Update(deltaTime);
            }
        }
    }

    /// <summary>
    /// Az előnézetek új mérete: azonnal érvényes (a régi képkockák nyújtva), a háttérben pedig az új méretű képkockák
    /// töltődnek be. A régi méretű, megosztott képkockákat a gyorsítótár elengedi.
    /// </summary>
    public void SetSize(int width, int height)
    {
        lock (_syncRoot)
        {
            if (_disposed || (width, height) == (_width, _height))
            {
                return;
            }

            (_width, _height) = (width, height);
            foreach (var preview in _entries.Keys)
            {
                preview.SetSize(width, height);
            }

            foreach (var key in _frames.Keys.Where(k => (k.Width, k.Height) != (width, height)).ToList())
            {
                _frames.Remove(key);
            }
        }

        _ = Task.Run(RescaleAsync, CancellationToken.None);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _entries.Clear();
            _frames.Clear();
        }

        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    /// <summary>A forrás betöltését meghatározó mezők; az azonosító és a név nem számít.</summary>
    private static string MediaKey(AnimationSource s) =>
        string.Join('|', s.SourceType, s.Path.Trim(), s.FrameWidth, s.FrameHeight, s.Columns, s.Rows, s.FrameCount);

    /// <summary>
    /// Azoknak az előnézeteknek, amelyek még nem az aktuális méretű képkockákat mutatják, betölti az új méretűeket.
    /// Ha a forrás az új méretre nem tölthető be, az előnézet a régi képkockákat mutatja nyújtva.
    /// </summary>
    private async Task RescaleAsync()
    {
        List<(IMutableAnimation Preview, Entry Entry, Task<IReadOnlyList<BitmapSource>> Loading)> pending;
        int width;
        int height;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            (width, height) = (_width, _height);
            pending = _entries
                .Where(e => e.Value.FramesSize != (width, height))
                .Select(e => (e.Key, e.Value, Frames(e.Value.Source, e.Value.Media, width, height)))
                .ToList();
        }

        foreach (var (preview, entry, loading) in pending)
        {
            IReadOnlyList<BitmapSource> frames;
            try
            {
                frames = await loading.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is MediaLoadException or OperationCanceledException)
            {
                continue;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Egy előnézet újraskálázása nem sikerült: {ex}");
                continue;
            }

            lock (_syncRoot)
            {
                // Közben eldobták, vagy újabb méret jött: azt a később indult újraskálázás tölti be.
                if (_entries.TryGetValue(preview, out var current) && ReferenceEquals(current, entry) &&
                    (width, height) == (_width, _height))
                {
                    preview.SetFrames(frames, width, height);
                    entry.FramesSize = (width, height);
                }
            }
        }
    }

    /// <summary>A forrás képkockái a megadott méretben; a sikeres vagy folyamatban lévő betöltést megosztja. Csak a zár alatt hívható.</summary>
    private Task<IReadOnlyList<BitmapSource>> Frames(AnimationSource source, string media, int width, int height)
    {
        var key = (media, width, height);
        if (!_frames.TryGetValue(key, out var loading) || loading.IsFaulted || loading.IsCanceled)
        {
            loading = _frameLoader.LoadFramesAsync(source, width, height, _lifetime.Token);
            _frames[key] = loading;
        }

        return loading;
    }

    /// <summary>Elengedi azokat a képkockákat, amelyeket már egyetlen előnézet sem használ, és senki sem vár. Csak a zár alatt hívható.</summary>
    private void DropUnusedFrames()
    {
        var used = _entries.Values.Select(e => e.Media).ToHashSet();
        foreach (var key in _frames.Keys.Where(k => !used.Contains(k.Media) && !_waiting.ContainsKey(k)).ToList())
        {
            _frames.Remove(key);
        }
    }

    /// <summary>Egy <see cref="CreateAsync"/> hívó már nem vár a betöltésre. Csak a zár alatt hívható.</summary>
    private void StopWaiting((string Media, int Width, int Height) key)
    {
        if (_waiting.TryGetValue(key, out int count))
        {
            if (count > 1)
            {
                _waiting[key] = count - 1;
            }
            else
            {
                _waiting.Remove(key);
            }
        }
    }

    /// <summary>Egy kiadott előnézet forrása és a képkockáinak mérete.</summary>
    private sealed class Entry(AnimationSource source, string media)
    {
        public AnimationSource Source { get; } = source;

        public string Media { get; } = media;

        /// <summary>A képkockák mérete; ha eltér az aktuálistól, az előnézet még a régi képkockákat mutatja nyújtva.</summary>
        public (int Width, int Height) FramesSize { get; set; }
    }
}
