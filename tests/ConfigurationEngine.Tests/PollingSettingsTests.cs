namespace ConfigurationEngine.Tests;

using ConfigurationEngine.Model;
using MonkeyStack.Core.Events;

/// <summary>
/// A <see cref="ConfigurationService"/> mint <see cref="IPollingSettingsProvider"/> (schema.md, "MetricPolling").
/// </summary>
public sealed class PollingSettingsTests : IDisposable
{
    private readonly FakeStore _store = new() { Data = TestData.ValidConfiguration() };
    private readonly ConfigurationService _service;
    private int _changes;

    public PollingSettingsTests()
    {
        _service = new ConfigurationService(_store, new FakeFrameLoader(), new FakeMediaValidator(), new ManualFrameLoop());
        Provider.PollingSettingsChanged += (_, _) => Interlocked.Increment(ref _changes);
    }

    private IPollingSettingsProvider Provider => _service;

    public void Dispose() => _service.Dispose();

    [Fact]
    public void BeforeLoad_SchemaDefaults()
    {
        foreach (var metric in Enum.GetValues<Metric>())
        {
            Assert.Equal(TimeSpan.FromMilliseconds(ConfigurationDefaults.PollingIntervalMs(metric)), Provider.GetPollingInterval(metric));
        }
    }

    [Fact]
    public async Task AfterLoad_FollowsConfiguration_DisabledIsNull()
    {
        Polling(Metric.Cpu).IntervalMs = 500;
        Polling(Metric.Battery).IsEnabled = false;

        await _service.InitializeAsync();

        Assert.Equal(TimeSpan.FromMilliseconds(500), Provider.GetPollingInterval(Metric.Cpu));
        Assert.Null(Provider.GetPollingInterval(Metric.Battery));
        Assert.Equal(1, _changes);
    }

    [Fact]
    public async Task MissingMetric_FallsBackToDefault()
    {
        _store.Data.MetricPollings.RemoveAll(p => p.Metric == Metric.Disk);

        await _service.InitializeAsync();

        Assert.Equal(TimeSpan.FromMilliseconds(ConfigurationDefaults.PollingIntervalMs(Metric.Disk)), Provider.GetPollingInterval(Metric.Disk));
    }

    [Fact]
    public async Task Changed_OnlyWhenPollingChanges()
    {
        await _service.InitializeAsync();
        Assert.Equal(0, _changes); // a tárolt beállítás az alapértékekkel egyezik

        _store.Data.BehaviorRules[0].Name = "Átnevezett";
        await _service.ReloadAsync();
        Assert.Equal(0, _changes);

        Polling(Metric.Memory).IntervalMs = 2000;
        await _service.ReloadAsync();
        Assert.Equal(1, _changes);
        Assert.Equal(TimeSpan.FromSeconds(2), Provider.GetPollingInterval(Metric.Memory));
    }

    [Fact]
    public async Task ThrowingSubscriber_DoesNotStopOthers()
    {
        int seen = 0;
        Provider.PollingSettingsChanged += (_, _) => throw new InvalidOperationException("hibás feliratkozó");
        Provider.PollingSettingsChanged += (_, _) => Interlocked.Increment(ref seen);
        Polling(Metric.Cpu).IsEnabled = false;

        await _service.InitializeAsync();

        Assert.Equal(1, seen);
    }

    private MetricPolling Polling(Metric metric) => _store.Data.MetricPollings.Single(p => p.Metric == metric);
}
