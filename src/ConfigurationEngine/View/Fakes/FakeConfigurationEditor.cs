namespace ConfigurationEngine.View.Fakes;

using System.Diagnostics;
using System.Windows.Media.Imaging;
using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using ConfigurationEngine.Storage;
using ConfigurationEngine.Validation;
using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Events;

/// <summary>
/// Memóriában tároló <see cref="IConfigurationEditor"/> a konfigurációs ablak önálló fejlesztéséhez és teszteléséhez
/// (view.md, "Alkalmazott minták": a View egy hamis facade-dal önállóan is tesztelhető). Akkor használja a
/// <see cref="MainWindow"/>, ha a kapott szolgáltatás még nem valósítja meg az <see cref="IConfigurationEditor"/>-t.
/// Az ellenőrzést és a képkockák betöltését a valódi <see cref="ConfigurationValidator"/>, <see cref="MediaValidator"/>
/// és <see cref="FrameLoader"/> végzi; csak a tárolás és az előnézetek léptetése egyszerűsített.
/// </summary>
internal sealed class FakeConfigurationEditor : IConfigurationEditor, IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(16);

    private readonly object _syncRoot = new();
    private readonly IConfigurationValidator _validator = new ConfigurationValidator();
    private readonly IMediaValidator _mediaValidator;
    private readonly IFrameLoader _frameLoader;
    private readonly Dictionary<Animation, AnimationSource> _previews = [];

    /// <summary>
    /// A betöltött képkockák forrásonként és méretenként: ugyanannak a forrásnak az előnézetei osztoznak rajtuk,
    /// így egy új előnézet (pl. sebességváltáskor) nem tölti be újra a forrást.
    /// </summary>
    private readonly Dictionary<(string Source, int Width, int Height), Task<IReadOnlyList<BitmapSource>>> _frames = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Timer _timer;
    private ConfigurationData _stored;
    private IReadOnlyList<DroppedItem> _droppedItems;
    private TimeSpan _lastTick;
    private int _width;
    private int _height;
    private bool _disposed;

    /// <param name="initial">A „tárolt” konfiguráció; alapértelmezés szerint <see cref="SampleData"/>.</param>
    /// <param name="droppedItems">Az első betöltéskor visszaadott kihagyott elemek; alapértelmezés szerint egy minta elem.</param>
    /// <param name="paths">A médiafájlok útvonalának feloldása (a tesztek ideiglenes mappát adhatnak meg).</param>
    public FakeConfigurationEditor(
        ConfigurationData? initial = null,
        IReadOnlyList<DroppedItem>? droppedItems = null,
        MediaPathResolver? paths = null)
    {
        _stored = (initial ?? SampleData()).Copy();
        _droppedItems = droppedItems ?? SampleDroppedItems();
        _width = _stored.Character.CharacterWidth;
        _height = _stored.Character.CharacterHeight;
        _mediaValidator = new MediaValidator(paths);
        _frameLoader = new FrameLoader(paths);

        // A motor UpdateAnimations időzítőjének megfelelője: az előnézeteket nem a View lépteti.
        _timer = new Timer(_ => Tick(), null, TickInterval, TickInterval);
    }

    /// <summary>A sikeres mentések száma (tesztekhez).</summary>
    public int SaveCount { get; private set; }

    /// <summary>A szolgáltatáson aktuálisan érvényes karakterméret (tesztekhez).</summary>
    public (int Width, int Height) CharacterSize
    {
        get
        {
            lock (_syncRoot)
            {
                return (_width, _height);
            }
        }
    }

    /// <summary>Hányszor kellett egy forrás képkockáit ténylegesen betölteni (tesztekhez; a megosztott betöltés nem számít).</summary>
    public int FrameLoadCount { get; private set; }

    /// <summary>A kiadott, még el nem dobott előnézetek száma (tesztekhez).</summary>
    public int PreviewCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _previews.Count;
            }
        }
    }

    public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            return Task.FromResult(new ConfigurationLoadResult(_stored.Copy(), _droppedItems, DatabaseWasReset: false));
        }
    }

    public ValidationResult Validate(ConfigurationData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var copy = data.Copy();
        ModelCopies.Normalize(copy);
        return _validator.Validate(copy);
    }

    public Task<IReadOnlyList<ValidationError>> ValidateMediaAsync(
        AnimationSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _mediaValidator.ValidateAsync(source.Copy(), MediaValidationDepth.Full, cancellationToken);
    }

    public async Task SaveAsync(ConfigurationData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var copy = data.Copy();
        ModelCopies.Normalize(copy);

        var errors = _validator.Validate(copy).Errors.ToList();
        foreach (var source in copy.AnimationSources)
        {
            errors.AddRange(await _mediaValidator.ValidateAsync(source, MediaValidationDepth.Full, cancellationToken));
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(new ValidationResult(errors));
        }

        AssignIds(copy);
        lock (_syncRoot)
        {
            _stored = copy.Copy();
            _droppedItems = [];
            SaveCount++;
        }

        ModelCopies.ApplySaved(copy, data);
        SetCharacterSize(copy.Character.CharacterWidth, copy.Character.CharacterHeight);
    }

    public void SetCharacterSize(int width, int height)
    {
        if (!ConfigurationLimits.CharacterSizePx.Contains(width) || !ConfigurationLimits.CharacterSizePx.Contains(height))
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"Érvénytelen karakterméret: {width} × {height}.");
        }

        KeyValuePair<Animation, AnimationSource>[] previews;
        lock (_syncRoot)
        {
            if (width == _width && height == _height)
            {
                return;
            }

            _width = width;
            _height = height;
            previews = [.. _previews];

            // A régi méretű képkockákra már nincs szükség.
            foreach (var key in _frames.Keys.Where(k => k.Width != width || k.Height != height).ToList())
            {
                _frames.Remove(key);
            }
        }

        foreach (var (preview, source) in previews)
        {
            _ = RescaleAsync(preview, source, width, height);
        }
    }

    public async Task<IAnimation> CreatePreviewAnimationAsync(
        AnimationSource source,
        double framesPerSecond,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy = source.Copy();
        var (width, height) = CharacterSize;
        var frames = await LoadFrames(copy, width, height).WaitAsync(cancellationToken);
        var preview = new Animation(frames, width, height, framesPerSecond);
        lock (_syncRoot)
        {
            _previews[preview] = copy;
        }

        return preview;
    }

    public void ReleasePreviewAnimation(IAnimation preview)
    {
        if (preview is Animation animation)
        {
            lock (_syncRoot)
            {
                _previews.Remove(animation);
            }
        }
    }

    public void ReleasePreviewAnimations()
    {
        lock (_syncRoot)
        {
            _previews.Clear();
            _frames.Clear();
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _disposed = true;
            _previews.Clear();
            _frames.Clear();
        }

        _timer.Dispose();
    }

    /// <summary>
    /// Minta konfiguráció a szállított alapanimációkkal és a schema.md "Kiértékelés" példájához hasonló
    /// szabályokkal. Az akkumulátor mintavételezése ki van kapcsolva, így a rá épülő szabály figyelmeztetést kap.
    /// </summary>
    public static ConfigurationData SampleData()
    {
        var pollings = ConfigurationDefaults.CreateMetricPollings();
        pollings.Single(p => p.Metric == Metric.Battery).IsEnabled = false;

        return new ConfigurationData
        {
            AnimationSources = ConfigurationDefaults.CreateMovementSources(),
            MovementAnimations = ConfigurationDefaults.CreateMovementAnimations(),
            BehaviorRules =
            [
                Rule(1, "Kis tűz", Metric.Cpu, ComparisonOperator.Gt, 60, sourceId: 3, zIndex: 20, group: "fire", priority: 10),
                Rule(2, "Nagy tűz", Metric.Cpu, ComparisonOperator.Gt, 80, sourceId: 4, zIndex: 20, group: "fire", priority: 20),
                Rule(3, "Pizsama", Metric.Time, ComparisonOperator.Between, 1230, sourceId: 1, zIndex: 10, upper: 360),
                Rule(4, "Lemerülés", Metric.Battery, ComparisonOperator.Lt, 20, sourceId: 2, zIndex: 5)
            ],
            Character = ConfigurationDefaults.CreateCharacterSettings(),
            MetricPollings = pollings
        };
    }

    /// <summary>Egy minta kihagyott szabály, hogy a figyelmeztető sáv önálló futásnál is kipróbálható legyen.</summary>
    public static IReadOnlyList<DroppedItem> SampleDroppedItems() =>
    [
        new DroppedItem(
            ConfigurationItemKind.BehaviorRule,
            "5",
            "Régi szabály",
            [
                new ValidationError(
                    ConfigurationItemKind.BehaviorRule,
                    "5",
                    nameof(BehaviorRule.AnimationSourceId),
                    "A hivatkozott animációs forrás (Id = 9) nem létezik.")
            ])
    ];

    private static BehaviorRule Rule(
        int id,
        string name,
        Metric metric,
        ComparisonOperator op,
        int threshold,
        int sourceId,
        int zIndex,
        int? upper = null,
        string? group = null,
        int priority = 0) => new()
        {
            Id = id,
            Name = name,
            Metric = metric,
            Operator = op,
            Threshold = threshold,
            ThresholdUpper = upper,
            AnimationSourceId = sourceId,
            ZIndex = zIndex,
            ExclusiveGroup = group,
            Priority = priority
        };

    /// <summary>
    /// A valódi tárolóhoz hasonlóan valódi azonosítót ad az új elemeknek, és átírja a negatív ideiglenes
    /// azonosítóra mutató hivatkozásokat (implementation.md, "Ideiglenes azonosítók").
    /// </summary>
    private static void AssignIds(ConfigurationData data)
    {
        var nextSourceId = data.AnimationSources.Select(s => s.Id).Append(0).Max() + 1;
        var sourceIds = new Dictionary<int, int>();
        foreach (var source in data.AnimationSources.Where(s => s.Id <= 0))
        {
            if (source.Id < 0)
            {
                sourceIds[source.Id] = nextSourceId;
            }

            source.Id = nextSourceId++;
        }

        foreach (var movement in data.MovementAnimations)
        {
            movement.AnimationSourceId = sourceIds.GetValueOrDefault(movement.AnimationSourceId, movement.AnimationSourceId);
        }

        var nextRuleId = data.BehaviorRules.Select(r => r.Id).Append(0).Max() + 1;
        foreach (var rule in data.BehaviorRules)
        {
            rule.AnimationSourceId = sourceIds.GetValueOrDefault(rule.AnimationSourceId, rule.AnimationSourceId);
            if (rule.Id <= 0)
            {
                rule.Id = nextRuleId++;
            }
        }
    }

    private async Task RescaleAsync(Animation preview, AnimationSource source, int width, int height)
    {
        try
        {
            var frames = await LoadFrames(source, width, height);
            lock (_syncRoot)
            {
                if (!_previews.ContainsKey(preview) || _width != width || _height != height)
                {
                    return;
                }
            }

            preview.SetFrames(frames, width, height);
        }
        catch (MediaLoadException)
        {
            // Az előnézet a régi méretben marad; a forrás hibáját a médiaellenőrzés jelzi.
        }
    }

    /// <summary>A forrás képkockái a megadott méreten; a sikeres vagy folyamatban lévő betöltést megosztja.</summary>
    private Task<IReadOnlyList<BitmapSource>> LoadFrames(AnimationSource source, int width, int height)
    {
        var key = (FrameKey(source), width, height);
        lock (_syncRoot)
        {
            if (!_frames.TryGetValue(key, out var frames) || frames.IsFaulted || frames.IsCanceled)
            {
                frames = _frameLoader.LoadFramesAsync(source, width, height);
                _frames[key] = frames;
                FrameLoadCount++;
            }

            return frames;
        }
    }

    /// <summary>A forrás betöltését meghatározó mezők; a név és az azonosító nem számít.</summary>
    private static string FrameKey(AnimationSource s) =>
        string.Join('|', s.SourceType, s.Path, s.FrameWidth, s.FrameHeight, s.Columns, s.Rows, s.FrameCount);

    private void Tick()
    {
        Animation[] previews;
        TimeSpan delta;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            var now = _clock.Elapsed;
            delta = now - _lastTick;
            _lastTick = now;
            previews = [.. _previews.Keys];
        }

        foreach (var preview in previews)
        {
            preview.Update(delta);
        }
    }
}
