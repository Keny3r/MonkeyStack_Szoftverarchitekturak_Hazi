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
                    loaded = await frames.LoadAsync(fallback, cancellationToken).ConfigureAwait(false);
                }

                framesPerSecond = ConfigurationDefaults.CreateMovementAnimation(state, fallback.Id).FramesPerSecond;
            }

            movement[state] = new Animation(loaded ?? [], width, height, framesPerSecond);
        }

        var rules = new List<RuleAnimation>();
        foreach (var rule in data.BehaviorRules.Where(r => r.IsEnabled))
        {
            if (sources.GetValueOrDefault(rule.AnimationSourceId) is { } source &&
                await frames.LoadAsync(source, cancellationToken).ConfigureAwait(false) is { } loaded)
            {
                rules.Add(new RuleAnimation(rule, new Animation(loaded, width, height, rule.FramesPerSecond, rule.ZIndex)));
            }
        }

        var disabledMetrics = data.MetricPollings.Where(p => !p.IsEnabled).Select(p => p.Metric).ToHashSet();
        return new AnimationSet(data, movement, rules, disabledMetrics, frames.Failures);
    }

    private static bool IsSameMedia(AnimationSource a, AnimationSource b) =>
        a.SourceType == b.SourceType &&
        string.Equals(a.Path.Trim().Replace('\\', '/'), b.Path, StringComparison.OrdinalIgnoreCase);

    /// <summary>Forrásonként egyszer tölt be; a nem betölthető forrást megjegyzi, és nem próbálja újra.</summary>
    private sealed class FrameCache(IFrameLoader loader, int width, int height)
    {
        private readonly Dictionary<AnimationSource, IReadOnlyList<BitmapSource>?> _loaded = new(ReferenceEqualityComparer.Instance);
        private readonly List<MediaLoadFailure> _failures = [];

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

            _loaded[source] = frames;
            return frames;
        }
    }
}
