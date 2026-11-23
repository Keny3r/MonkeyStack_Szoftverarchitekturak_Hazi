namespace OsEventEngine.Tests;

using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;
using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;

public class MonitorTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private static (MetricMonitor Monitor, Channel<MetricSample> Output, FakeTimeProvider Time) Create(FakeSource source)
    {
        var output = Channel.CreateUnbounded<MetricSample>();
        var time = new FakeTimeProvider();
        return (new MetricMonitor(source, output.Writer, time), output, time);
    }

    [Fact]
    public async Task SamplesImmediatelyThenEveryInterval()
    {
        var source = new FakeSource(Metric.Memory, 40);
        var (monitor, output, time) = Create(source);

        monitor.Init(new MonitorOptions(true, Interval));
        Assert.True(await Eventually.UntilAsync(() => source.Calls >= 1));
        Assert.True(await Eventually.UntilAsync(() => source.Calls >= 3, tick: () => time.Advance(Interval)));
        Assert.True(output.Reader.TryRead(out var sample));
        Assert.Equal(new MetricSample(Metric.Memory, 40, SampleOrigin.Live, sample.Timestamp), sample);

        monitor.Fini();
    }

    [Fact]
    public async Task TriggerNowSamplesWithoutAdvancingTime()
    {
        var source = new FakeSource(Metric.Memory, 40);
        var (monitor, _, _) = Create(source);
        monitor.Init(new MonitorOptions(true, TimeSpan.FromMinutes(1)));
        Assert.True(await Eventually.UntilAsync(() => source.Calls == 1));

        monitor.TriggerNow();

        Assert.True(await Eventually.UntilAsync(() => source.Calls == 2));
        monitor.Fini();
    }

    [Fact]
    public async Task SuspendStopsSamplingAndResumeContinues()
    {
        var source = new FakeSource(Metric.Memory, 40);
        var (monitor, _, time) = Create(source);
        monitor.Init(new MonitorOptions(true, Interval));
        Assert.True(await Eventually.UntilAsync(() => source.Calls >= 1));

        monitor.Suspend();
        await Task.Delay(50);
        int callsWhenSuspended = source.Calls;
        for (int i = 0; i < 5; i++)
        {
            time.Advance(Interval);
            await Task.Delay(10);
        }

        Assert.Equal(MonitorStatus.Suspended, monitor.Status);
        Assert.Equal(callsWhenSuspended, source.Calls);

        monitor.Resume();
        Assert.True(await Eventually.UntilAsync(() => source.Calls > callsWhenSuspended));
        Assert.Equal(MonitorStatus.Running, monitor.Status);
        monitor.Fini();
    }

    [Fact]
    public async Task DisabledOptionsStartSuspended()
    {
        var source = new FakeSource(Metric.Memory, 40);
        var (monitor, _, _) = Create(source);

        monitor.Init(new MonitorOptions(false, Interval));
        await Task.Delay(100);

        Assert.Equal(MonitorStatus.Suspended, monitor.Status);
        Assert.Equal(0, source.Calls);
        monitor.Fini();
    }

    [Fact]
    public async Task FailingSourceIsFaultedAndRecovers()
    {
        bool fail = true;
        var source = new FakeSource(Metric.Memory, () => fail ? throw new InvalidOperationException("teszt") : 40);
        var (monitor, output, time) = Create(source);

        monitor.Init(new MonitorOptions(true, Interval));
        Assert.True(await Eventually.UntilAsync(() => monitor.Status == MonitorStatus.Faulted));
        Assert.Contains("hiba", monitor.Info());

        fail = false;
        Assert.True(await Eventually.UntilAsync(() => monitor.Status == MonitorStatus.Running, tick: () => time.Advance(Interval)));
        Assert.True(output.Reader.TryRead(out _));
        monitor.Fini();
    }

    [Fact]
    public async Task NullSampleProducesNoOutput()
    {
        var source = new FakeSource(Metric.Battery, () => null);
        var (monitor, output, _) = Create(source);

        monitor.Init(new MonitorOptions(true, Interval));
        Assert.True(await Eventually.UntilAsync(() => source.Calls >= 1));

        Assert.False(output.Reader.TryRead(out _));
        monitor.Fini();
    }
}
