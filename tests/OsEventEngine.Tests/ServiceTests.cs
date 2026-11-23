namespace OsEventEngine.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;
using OsEventEngine.Simulation;
using OsEventEngine.Sources;

public class ServiceTests
{
    /// <summary>Hat metrika fix értékkel; az akkumulátor nem elérhető (asztali gép).</summary>
    private static IEnumerable<IMetricSource> FakeSources(TimeProvider _) =>
    [
        new FakeSource(Metric.Cpu, 30),
        new FakeSource(Metric.Memory, 60),
        new FakeSource(Metric.Disk, 70),
        new FakeSource(Metric.Battery, () => null),
        new FakeSource(Metric.WindowCount, 5),
        new FakeSource(Metric.Time, (20 * 60) + 30),
        new FakeSource(Metric.Inactivity, 2),
    ];

    private static OsEventMonitorService CreateService(OsEventNotifier notifier, OsEventEngineOptions? options = null) =>
        new(notifier, options ?? OsEventEngineOptions.Default with { EnableSmoothing = false }, new FakeTimeProvider(), FakeSources);

    private static ConcurrentQueue<string> Record(OsEventNotifier n)
    {
        var events = new ConcurrentQueue<string>();
        n.CpuChanged += (_, e) => events.Enqueue($"Cpu={e.UsagePercent}");
        n.MemoryChanged += (_, e) => events.Enqueue($"Memory={e.UsagePercent}");
        n.DiskChanged += (_, e) => events.Enqueue($"Disk={e.UsagePercent}");
        n.BatteryChanged += (_, e) => events.Enqueue($"Battery={e.ChargePercent}");
        n.WindowCountChanged += (_, e) => events.Enqueue($"WindowCount={e.WindowCount}");
        n.TimeChanged += (_, e) => events.Enqueue($"Time={e.Hour:00}:{e.Minute:00}");
        n.InactivityChanged += (_, e) => events.Enqueue($"Inactivity={e.InactivityMinutes}");
        return events;
    }

    [Fact]
    public async Task StartSendsInitialSnapshotForAvailableMetrics()
    {
        var notifier = new OsEventNotifier();
        var events = Record(notifier);
        using var service = CreateService(notifier);

        service.Start(context: null);

        Assert.True(await Eventually.UntilAsync(() => events.Count >= 6));
        await Task.Delay(100);
        Assert.Equal(
            ["Cpu=30", "Disk=70", "Inactivity=2", "Memory=60", "Time=20:30", "WindowCount=5"],
            events.Order().ToArray());

        var snapshot = service.GetSnapshot();
        Assert.Null(snapshot.Battery);
        Assert.Equal(20, snapshot.Time!.Hour);
    }

    [Fact]
    public async Task SimulatedValueReachesSubscribersQuickly()
    {
        var notifier = new OsEventNotifier();
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        notifier.CpuChanged += (_, e) =>
        {
            if (e.UsagePercent == 95)
            {
                received.TrySetResult(e.UsagePercent);
            }
        };
        using var service = CreateService(notifier);
        service.Start(context: null);
        Assert.True(await Eventually.UntilAsync(() => service.GetSnapshot().Cpu is not null));

        var stopwatch = Stopwatch.StartNew();
        service.Simulation.SetSimulatedValue(Metric.Cpu, 95);
        var completed = await Task.WhenAny(received.Task, Task.Delay(2000));

        Assert.Same(received.Task, completed);
        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"{stopwatch.ElapsedMilliseconds} ms");
        Assert.True(service.TryGetSentSample(Metric.Cpu, out var sent));
        Assert.Equal(SampleOrigin.Simulated, sent.Origin);
    }

    [Fact]
    public async Task SimulationSetBeforeStartIsUsedAtStart()
    {
        var notifier = new OsEventNotifier();
        var events = Record(notifier);
        using var service = CreateService(notifier);

        service.Simulation.SetSimulatedValue(Metric.Battery, 5);
        service.Start(context: null);

        Assert.True(await Eventually.UntilAsync(() => events.Contains("Battery=5")));
    }

    [Fact]
    public async Task IsMockModeEnabledTogglesAllMetrics()
    {
        using var service = CreateService(new OsEventNotifier());
        service.Start(context: null);
        Assert.True(await Eventually.UntilAsync(() => service.GetSnapshot().Memory is not null));

        service.IsMockModeEnabled = true;
        Assert.All(MetricCatalog.All, m => Assert.Equal(SourceMode.Simulated, service.Simulation.GetMode(m)));

        service.IsMockModeEnabled = false;
        Assert.False(service.IsMockModeEnabled);
    }

    [Fact]
    public async Task ReconfigureDisablesMetricWithoutRestart()
    {
        using var service = CreateService(new OsEventNotifier());
        service.Start(context: null);

        var monitors = MetricCatalog.All.ToDictionary(m => m, m => service.Options.For(m));
        monitors[Metric.Memory] = monitors[Metric.Memory] with { Enabled = false };
        service.Reconfigure(service.Options with { Monitors = monitors });

        Assert.True(await Eventually.UntilAsync(() => service.GetMonitorStatus(Metric.Memory) == MonitorStatus.Suspended));
        Assert.Equal(MonitorStatus.Running, service.GetMonitorStatus(Metric.Cpu));
    }

    [Fact]
    public async Task PollingSettingsProviderDrivesIntervalsAndEnabledState()
    {
        using var service = CreateService(new OsEventNotifier());
        var provider = new FakePollingSettings();
        service.UsePollingSettings(provider);
        service.Start(context: null);

        Assert.Equal(TimeSpan.FromSeconds(5), service.Options.For(Metric.Disk).Interval);

        provider.DisabledMetric = Metric.WindowCount;
        provider.Raise();

        Assert.True(await Eventually.UntilAsync(() => service.GetMonitorStatus(Metric.WindowCount) == MonitorStatus.Suspended));
        Assert.False(service.Options.For(Metric.WindowCount).Enabled);
    }

    [Fact]
    public async Task EventsArePostedToTheSynchronizationContext()
    {
        var context = new CountingContext();
        var notifier = new OsEventNotifier();
        var events = Record(notifier);
        using var service = CreateService(notifier);

        service.Start(context);

        Assert.True(await Eventually.UntilAsync(() => events.Count >= 6));
        Assert.True(context.Posts >= 6);
    }

    [Fact]
    public async Task ThrowingSubscriberDoesNotStopTheEngine()
    {
        var notifier = new OsEventNotifier();
        notifier.CpuChanged += (_, _) => throw new InvalidOperationException("hibás feliratkozó");
        var events = Record(notifier);
        using var service = CreateService(notifier);

        service.Start(context: null);

        // A dobó feliratkozó miatt a CPU-esemény többi feliratkozója nem fut le (C# multicast), de a motor
        // elkapja a kivételt, és a többi metrika, valamint a későbbi változások is kimennek.
        Assert.True(await Eventually.UntilAsync(() => events.Count >= 5));
        service.Simulation.SetSimulatedValue(Metric.Memory, 99);
        Assert.True(await Eventually.UntilAsync(() => events.Contains("Memory=99")));
    }

    [Fact]
    public void StartTwiceThrowsAndDisposeIsIdempotent()
    {
        var service = CreateService(new OsEventNotifier());
        service.Start(context: null);

        Assert.Throws<InvalidOperationException>(() => service.Start(context: null));

        var stopwatch = Stopwatch.StartNew();
        service.Dispose();
        service.Dispose();

        Assert.False(service.IsRunning);
        Assert.True(stopwatch.ElapsedMilliseconds < 1500);
        Assert.Throws<ObjectDisposedException>(() => service.Start(context: null));
    }

    private sealed class FakePollingSettings : IPollingSettingsProvider
    {
        public Metric? DisabledMetric { get; set; }

        public event EventHandler? PollingSettingsChanged;

        public TimeSpan? GetPollingInterval(Metric metric) =>
            metric == DisabledMetric ? null
            : metric == Metric.Disk ? TimeSpan.FromSeconds(5)
            : TimeSpan.FromSeconds(1);

        public void Raise() => PollingSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class CountingContext : SynchronizationContext
    {
        private int _posts;

        public int Posts => Volatile.Read(ref _posts);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _posts);
            base.Post(d, state);
        }
    }
}
