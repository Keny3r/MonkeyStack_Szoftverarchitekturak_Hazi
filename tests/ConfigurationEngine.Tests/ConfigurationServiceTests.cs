namespace ConfigurationEngine.Tests;

using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using ConfigurationEngine.Storage;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

public sealed class ConfigurationServiceTests : IDisposable
{
    private readonly FakeStore _store = new();
    private readonly FakeFrameLoader _loader = new();
    private readonly ConfigurationService _service;

    public ConfigurationServiceTests()
    {
        _service = new ConfigurationService(_store, _loader);
    }

    public void Dispose() => _service.Dispose();

    [Fact]
    public void BeforeInitialize_NoAnimations()
    {
        Assert.Null(_service.GetMovementAnimation(MovementState.Idle));
        Assert.Empty(_service.GetOverlayAnimations(new OsEventDataRegistry { Cpu = new CpuEventData { UsagePercent = 99 } }));
    }

    [Fact]
    public async Task Initialize_BuildsMovementAndRuleAnimations()
    {
        _store.Data = TestData.ValidConfiguration();
        _store.Data.Character.CharacterWidth = 64;
        _store.Data.Character.CharacterHeight = 48;

        await _service.InitializeAsync();

        Assert.Equal((64, 48), (_service.CharacterWidth, _service.CharacterHeight));
        var walking = Assert.IsAssignableFrom<MonkeyStack.Core.Animation.IAnimation>(_service.GetMovementAnimation(MovementState.Walking));
        Assert.Equal(12, walking.FramesPerSecond);
        Assert.Equal((64, 48), (walking.GetWidth(), walking.GetHeight()));
        Assert.Equal((64, 48), (walking.CurrentFrame!.PixelWidth, walking.CurrentFrame.PixelHeight));

        var overlays = _service.GetOverlayAnimations(Registry(cpu: 85, hour: 22));
        Assert.Equal([10, 20], overlays.Select(a => a.ZIndex));
        Assert.Equal(_loader.FrameCountOf("pyjama"), overlays[0].FrameCount);
        Assert.Empty(_service.MediaLoadFailures);
    }

    [Fact]
    public async Task SharedSource_IsLoadedOnce()
    {
        _store.Data = TestData.ValidConfiguration();

        await _service.InitializeAsync();

        // A walk.gif-et négy mozgásállapot, a fire.png-t két szabály használja.
        Assert.Equal(1, _loader.CallsFor("walk.gif"));
        Assert.Equal(1, _loader.CallsFor("fire.png"));
    }

    [Fact]
    public async Task FailingMovementSource_FallsBackToShippedAnimation()
    {
        _store.Data = TestData.ValidConfiguration();
        _loader.Failing.Add("walk.gif");

        await _service.InitializeAsync();

        var idle = _service.GetMovementAnimation(MovementState.Idle)!;
        Assert.Equal(_loader.FrameCountOf("movement/idle"), idle.FrameCount);
        Assert.Equal(10, idle.FramesPerSecond); // az alapanimáció sebessége, nem a beállított 8
        var failure = Assert.Single(_service.MediaLoadFailures);
        Assert.Equal((1, "Séta"), (failure.AnimationSourceId, failure.SourceName));
    }

    [Fact]
    public async Task FailingShippedAnimation_LeavesEmptyAnimation()
    {
        _store.Data = TestData.ValidConfiguration();
        _loader.Failing.Add("walk.gif");
        _loader.Failing.Add("movement/falling");

        await _service.InitializeAsync();

        var falling = _service.GetMovementAnimation(MovementState.Falling);
        Assert.NotNull(falling);
        Assert.Null(falling.CurrentFrame);
        Assert.Equal(2, _service.MediaLoadFailures.Count);
    }

    [Fact]
    public async Task MissingMovementRow_UsesShippedAnimation()
    {
        _store.Data = TestData.ValidConfiguration();
        _store.Data.MovementAnimations.RemoveAll(m => m.MovementState == MovementState.Dragging);

        await _service.InitializeAsync();

        Assert.Equal(_loader.FrameCountOf("movement/dragging"), _service.GetMovementAnimation(MovementState.Dragging)!.FrameCount);
    }

    [Fact]
    public async Task FailingRuleSource_OmitsRule()
    {
        _store.Data = TestData.ValidConfiguration();
        _loader.Failing.Add("fire.png");

        await _service.InitializeAsync();

        var overlay = Assert.Single(_service.GetOverlayAnimations(Registry(cpu: 85, hour: 22)));
        Assert.Equal(10, overlay.ZIndex);
        Assert.Equal(2, _service.MediaLoadFailures.Single().AnimationSourceId);
    }

    [Fact]
    public async Task DisabledPolling_DisablesItsRules()
    {
        _store.Data = TestData.ValidConfiguration();
        _store.Data.MetricPollings.Single(p => p.Metric == Metric.Time).IsEnabled = false;

        await _service.InitializeAsync();

        Assert.Equal([20], _service.GetOverlayAnimations(Registry(cpu: 85, hour: 22)).Select(a => a.ZIndex));
    }

    [Fact]
    public async Task StoreChanged_ReloadsAndRaisesConfigurationChanged()
    {
        _store.Data = TestData.ValidConfiguration();
        await _service.InitializeAsync();
        var before = _service.GetMovementAnimation(MovementState.Idle);

        var changed = WaitForChange();
        _store.Data.MovementAnimations.Single(m => m.MovementState == MovementState.Idle).FramesPerSecond = 30;
        _store.RaiseChanged();
        await changed;

        var after = _service.GetMovementAnimation(MovementState.Idle)!;
        Assert.NotSame(before, after);
        Assert.Equal(30, after.FramesPerSecond);
    }

    [Fact]
    public async Task FailingReload_KeepsRunningConfiguration()
    {
        _store.Data = TestData.ValidConfiguration();
        await _service.InitializeAsync();
        var before = _service.GetMovementAnimation(MovementState.Idle);

        _store.Fail = true;

        await Assert.ThrowsAsync<IOException>(() => _service.ReloadAsync());
        Assert.Same(before, _service.GetMovementAnimation(MovementState.Idle));
    }

    [Fact]
    public async Task SetCharacterSize_RescalesFramesInPlace()
    {
        _store.Data = TestData.ValidConfiguration();
        await _service.InitializeAsync();
        var idle = _service.GetMovementAnimation(MovementState.Idle)!;
        int raised = 0;
        _service.ConfigurationChanged += (_, _) => Interlocked.Increment(ref raised);

        var rescale = _service.SetCharacterSizeAsync(200, 150);

        // Azonnal az új méretben jelenik meg, egyetlen eseménnyel.
        Assert.Equal((200, 150), (idle.GetWidth(), idle.GetHeight()));
        Assert.Equal(1, raised);

        await rescale;

        Assert.Same(idle, _service.GetMovementAnimation(MovementState.Idle));
        Assert.Equal((200, 150), (idle.CurrentFrame!.PixelWidth, idle.CurrentFrame.PixelHeight));
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task SetCharacterSize_IsClampedAndIgnoresUnchangedSize()
    {
        _store.Data = TestData.ValidConfiguration();
        await _service.InitializeAsync();
        int raised = 0;
        _service.ConfigurationChanged += (_, _) => Interlocked.Increment(ref raised);

        await _service.SetCharacterSizeAsync(10_000, 0);

        Assert.Equal((ConfigurationLimits.CharacterSizePx.Max, 100), (_service.CharacterWidth, _service.CharacterHeight));
        await _service.SetCharacterSizeAsync(ConfigurationLimits.CharacterSizePx.Max, 100);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Reload_RestoresSavedSize()
    {
        _store.Data = TestData.ValidConfiguration();
        await _service.InitializeAsync();
        await _service.SetCharacterSizeAsync(300, 300);

        await _service.ReloadAsync();

        Assert.Equal((100, 100), (_service.CharacterWidth, _service.CharacterHeight));
    }

    [Fact]
    public async Task UpdateAnimations_StepsAllAnimations()
    {
        _store.Data = TestData.ValidConfiguration();
        await _service.InitializeAsync();
        var walking = _service.GetMovementAnimation(MovementState.Walking)!;   // 12 képkocka/s
        var fire = _service.GetOverlayAnimations(Registry(cpu: 85))[0];         // 10 képkocka/s

        _service.UpdateAnimations(TimeSpan.FromSeconds(0.1));

        Assert.Equal(1, walking.CurrentFrameIndex);
        Assert.Equal(1, fire.CurrentFrameIndex);
    }

    private Task WaitForChange()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.ConfigurationChanged += (_, _) => tcs.TrySetResult();
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static OsEventDataRegistry Registry(int? cpu = null, int? hour = null) => new()
    {
        Cpu = cpu is { } c ? new CpuEventData { UsagePercent = c } : null,
        Time = hour is { } h ? new TimeEventData { Hour = h } : null
    };

    private sealed class FakeStore : IConfigurationStore
    {
        public ConfigurationData Data { get; set; } = new();

        public bool Fail { get; set; }

        public event EventHandler? Changed;

        public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Fail
                ? Task.FromException<ConfigurationLoadResult>(new IOException("zárolt adatbázis"))
                : Task.FromResult(new ConfigurationLoadResult(ModelCopies.Copy(Data), [], false));

        public Task SaveAsync(ConfigurationData data, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Útvonalanként eltérő számú, a kért méretű üres képkockát ad; a <see cref="Failing"/> útvonalakra hibát dob.</summary>
    private sealed class FakeFrameLoader : IFrameLoader
    {
        private readonly ConcurrentDictionary<string, int> _calls = new();

        public HashSet<string> Failing { get; } = [];

        public int FrameCountOf(string path) => 2 + (Math.Abs(string.GetHashCode(path, StringComparison.Ordinal)) % 5);

        public int CallsFor(string path) => _calls.GetValueOrDefault(path);

        public Task<IReadOnlyList<BitmapSource>> LoadFramesAsync(
            AnimationSource source,
            int width,
            int height,
            CancellationToken cancellationToken = default)
        {
            _calls.AddOrUpdate(source.Path, 1, (_, n) => n + 1);
            if (Failing.Contains(source.Path))
            {
                throw new MediaLoadException($"{source.Path} nem található.") { PropertyName = nameof(AnimationSource.Path) };
            }

            var frame = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, new byte[width * height * 4], width * 4);
            frame.Freeze();
            return Task.FromResult<IReadOnlyList<BitmapSource>>(Enumerable.Repeat(frame, FrameCountOf(source.Path)).ToArray());
        }
    }
}
