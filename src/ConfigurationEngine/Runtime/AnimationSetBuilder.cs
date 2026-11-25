namespace ConfigurationEngine.Runtime;

using System.Windows.Media.Imaging;
using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;

/// <summary>
/// A betöltött konfigurációból felépíti a futó animációkat (schema.md, "Animációk előállítása").
/// Minden forrást egyszer tölt be, és a képkockákat a rá hivatkozó animációk megosztják.
/// </summary>
internal sealed class AnimationSetBuilder
{
    private readonly IFrameLoader _frameLoader;

    public AnimationSetBuilder(IFrameLoader frameLoader)
    {
        _frameLoader = frameLoader;
    }

    /// <summary>
    /// Felépíti az animációkat <paramref name="width"/> × <paramref name="height"/> méretben:
    /// <list type="bullet">
    /// <item>mozgásállapotonként egyet; ha a sor hiányzik vagy a forrása nem tölthető be, a szállított
    /// alapanimációt (ha az sem, üres animációt, hogy az állapot sose maradjon animáció nélkül);</item>
    /// <item>minden bekapcsolt szabályhoz egyet; a nem betölthető forrású szabály kimarad.</item>
    /// </list>
    /// A nem betölthető forrásokat az <see cref="AnimationSet.Failures"/> sorolja fel. A <paramref name="data"/>-t
    /// a pillanatkép megtartja, ezért a hívó utána nem módosíthatja.
    /// </summary>
    public async Task<AnimationSet> BuildAsync(ConfigurationData data, int width, int height, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);

        var frames = new FrameCache(_frameLoader, width, height);
        var sources = new Dictionary<int, AnimationSource>();
        foreach (var source in data.AnimationSources)
        {
            sources.TryAdd(source.Id, source);
        }

        var movement = new Dictionary<MovementState, IMutableAnimation>();
        var shown = new Dictionary<IMutableAnimation, AnimationSource>(ReferenceEqualityComparer.Instance);
        foreach (var state in Enum.GetValues<MovementState>())
        {
            var row = data.MovementAnimations.FirstOrDefault(m => m.MovementState == state);
            var configured = row is not null ? sources.GetValueOrDefault(row.AnimationSourceId) : null;
            var fallback = ConfigurationDefaults.CreateMovementSource(state);
            var source = configured ?? fallback;
            var loaded = await frames.LoadAsync(source, cancellationToken).ConfigureAwait(false);
            double framesPerSecond = row?.FramesPerSecond ?? 0;

            if (configured is null || loaded is null)
            {
                // A szállított alapanimáció a saját alapsebességével: a felhasználó sebessége a saját képeihez készült.
                // Ha a beállított forrás maga a szállított mappa, nem próbáljuk újra.
                if (loaded is null && !IsSameMedia(source, fallback))
                {
                    source = fallback;
                    loaded = await frames.LoadAsync(fallback, cancellationToken).ConfigureAwait(false);
                }

                framesPerSecond = ConfigurationDefaults.CreateMovementAnimation(state, fallback.Id).FramesPerSecond;
            }

            var animation = new Animation(loaded ?? [], width, height, framesPerSecond);
            movement[state] = animation;
            if (loaded is not null)
            {
                shown[animation] = source;
            }
        }

        var rules = new List<RuleAnimation>();
        foreach (var rule in data.BehaviorRules.Where(r => r.IsEnabled))
        {
            if (sources.GetValueOrDefault(rule.AnimationSourceId) is { } source &&
                await frames.LoadAsync(source, cancellationToken).ConfigureAwait(false) is { } loaded)
            {
                var animation = new Animation(loaded, width, height, rule.FramesPerSecond, rule.ZIndex);
                rules.Add(new RuleAnimation(rule, animation));
                shown[animation] = source;
            }
        }

        var disabledMetrics = data.MetricPollings.Where(p => !p.IsEnabled).Select(p => p.Metric).ToHashSet();
        return new AnimationSet(data, movement, rules, disabledMetrics, frames.Failures, shown);
    }

    /// <summary>
    /// Betölti a <paramref name="set"/> animációinak képkockáit <paramref name="width"/> × <paramref name="height"/>
    /// méretben, ugyanabból a forrásból, amelyet most mutatnak, és ugyanazzal a memóriakerettel, mint a felépítés.
    /// Az animációkat nem módosítja: a hívó a zár alatt adja át nekik a képkockákat
    /// (<see cref="IMutableAnimation.SetFrames"/>). A most nem betölthető forrás animációja kimarad a
    /// <see cref="Rescaled.Frames"/>-ből, a forrás pedig a <see cref="Rescaled.Failures"/>-be kerül.
    /// </summary>
    public async Task<Rescaled> RescaleAsync(AnimationSet set, int width, int height, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);

        var frames = new FrameCache(_frameLoader, width, height);
        var rescaled = new List<(IMutableAnimation, IReadOnlyList<BitmapSource>)>();

        // Az Animations sorrendjében (előbb a mozgásállapotok), így a memóriakeretbe ugyanazok férnek bele elsőként.
        foreach (var animation in set.Animations)
        {
            if (set.Sources.TryGetValue(animation, out var source) &&
                await frames.LoadAsync(source, cancellationToken).ConfigureAwait(false) is { } loaded)
            {
                rescaled.Add((animation, loaded));
            }
        }

        return new Rescaled(rescaled, frames.Failures);
    }

    /// <summary>Az új méretű képkockák animációnként, és a most nem betölthető források.</summary>
    internal sealed record Rescaled(
        IReadOnlyList<(IMutableAnimation Animation, IReadOnlyList<BitmapSource> Frames)> Frames,
        IReadOnlyList<MediaLoadFailure> Failures);

    private static bool IsSameMedia(AnimationSource a, AnimationSource b) =>
        a.SourceType == b.SourceType &&
        string.Equals(a.Path.Trim().Replace('\\', '/'), b.Path, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Forrásonként egyszer tölt be; a nem betölthető forrást megjegyzi, és nem próbálja újra. A betöltött képkockák
    /// együttes méretét a <see cref="ConfigurationLimits.MediaMaxTotalLoadedBytes"/> korlátozza: az a forrás, amely már
    /// nem fér bele, nem betölthetőnek számít. A mozgásállapotok töltődnek be először, így ők férnek bele elsőként.
    /// </summary>
    private sealed class FrameCache(IFrameLoader loader, int width, int height)
    {
        private readonly Dictionary<AnimationSource, IReadOnlyList<BitmapSource>?> _loaded = new(ReferenceEqualityComparer.Instance);
        private readonly List<MediaLoadFailure> _failures = [];
        private long _totalBytes;

        public IReadOnlyList<MediaLoadFailure> Failures => _failures;

        public async Task<IReadOnlyList<BitmapSource>?> LoadAsync(AnimationSource source, CancellationToken cancellationToken)
        {
            if (_loaded.TryGetValue(source, out var cached))
            {
                return cached;
            }

            IReadOnlyList<BitmapSource>? frames;
            try
            {
                frames = await loader.LoadFramesAsync(source, width, height, cancellationToken).ConfigureAwait(false);
            }
            catch (MediaLoadException ex)
            {
                _failures.Add(new MediaLoadFailure(source.Id, source.Name, ex.Message, ex.PropertyName));
                frames = null;
            }

            if (frames is not null)
            {
                long bytes = (long)frames.Count * width * height * 4;
                if (_totalBytes + bytes > ConfigurationLimits.MediaMaxTotalLoadedBytes)
                {
                    long limitMb = ConfigurationLimits.MediaMaxTotalLoadedBytes / (1024 * 1024);
                    _failures.Add(new MediaLoadFailure(
                        source.Id,
                        source.Name,
                        $"{width} × {height} px karakterméreten az animációk együtt legfeljebb {limitMb} MB memóriát foglalhatnak; ez a forrás már nem fér bele.",
                        null));
                    frames = null;
                }
                else
                {
                    _totalBytes += bytes;
                }
            }

            _loaded[source] = frames;
            return frames;
        }
    }
}
