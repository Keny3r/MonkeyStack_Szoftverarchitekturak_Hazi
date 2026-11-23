namespace OsEventEngine.Tests;

using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;
using OsEventEngine.Pipeline;
using OsEventEngine.State;

public class PipelineTests
{
    private static MetricSample Live(Metric metric, int value) => new(metric, value, SampleOrigin.Live, DateTimeOffset.UnixEpoch);

    private static MetricSample Simulated(Metric metric, int value) => new(metric, value, SampleOrigin.Simulated, DateTimeOffset.UnixEpoch);

    [Fact]
    public void ChangeFilter_FirstSamplePerMetricAlwaysPasses()
    {
        var filter = new ChangeFilterInterceptor(_ => 5);

        Assert.NotNull(filter.Intercept(Live(Metric.Cpu, 10)));
        Assert.NotNull(filter.Intercept(Live(Metric.Memory, 10)));
    }

    [Fact]
    public void ChangeFilter_DropsChangesBelowDeadband()
    {
        var filter = new ChangeFilterInterceptor(_ => 5);
        filter.Intercept(Live(Metric.Cpu, 50));

        Assert.Null(filter.Intercept(Live(Metric.Cpu, 50)));
        Assert.Null(filter.Intercept(Live(Metric.Cpu, 54)));
        Assert.NotNull(filter.Intercept(Live(Metric.Cpu, 55)));
    }

    [Fact]
    public void ChangeFilter_ComparesToLastSentValueNotLastSeen()
    {
        var filter = new ChangeFilterInterceptor(_ => 5);
        filter.Intercept(Live(Metric.Cpu, 50));
        filter.Intercept(Live(Metric.Cpu, 53));

        // 50 → 53 → 56: a lassú kúszás is kimegy, ha az utolsó küldötthöz képest eléri a deadbandet.
        Assert.NotNull(filter.Intercept(Live(Metric.Cpu, 56)));
    }

    [Fact]
    public void ChangeFilter_OriginSwitchAlwaysPasses()
    {
        var filter = new ChangeFilterInterceptor(_ => 1);
        filter.Intercept(Live(Metric.Cpu, 40));

        Assert.NotNull(filter.Intercept(Simulated(Metric.Cpu, 40)));
        Assert.NotNull(filter.Intercept(Live(Metric.Cpu, 40)));
    }

    [Fact]
    public void ChangeFilter_DeadbandBelowOneIsTreatedAsOne()
    {
        var filter = new ChangeFilterInterceptor(_ => 0);
        filter.Intercept(Live(Metric.Cpu, 40));

        Assert.Null(filter.Intercept(Live(Metric.Cpu, 40)));
    }

    [Fact]
    public void Smoothing_AppliesMovingAverageToLiveCpu()
    {
        var smoothing = new SmoothingInterceptor(() => true, alpha: 0.5);

        Assert.Equal(20, smoothing.Intercept(Live(Metric.Cpu, 20))!.Value.Value);
        Assert.Equal(60, smoothing.Intercept(Live(Metric.Cpu, 100))!.Value.Value);
    }

    [Fact]
    public void Smoothing_PassesSimulatedAndOtherMetricsUnchanged()
    {
        var smoothing = new SmoothingInterceptor(() => true, alpha: 0.5);
        smoothing.Intercept(Live(Metric.Cpu, 0));

        Assert.Equal(95, smoothing.Intercept(Simulated(Metric.Cpu, 95))!.Value.Value);
        Assert.Equal(77, smoothing.Intercept(Live(Metric.Memory, 77))!.Value.Value);

        // A szimuláció újraindítja a simítást: élőre visszaváltva nincs elhúzódó átmenet.
        Assert.Equal(10, smoothing.Intercept(Live(Metric.Cpu, 10))!.Value.Value);
    }

    [Fact]
    public void Smoothing_CanBeDisabled()
    {
        var smoothing = new SmoothingInterceptor(() => false);
        smoothing.Intercept(Live(Metric.Cpu, 0));

        Assert.Equal(100, smoothing.Intercept(Live(Metric.Cpu, 100))!.Value.Value);
    }

    [Fact]
    public void Chain_StopsWhenAnInterceptorDrops()
    {
        var store = new MetricSnapshotStore();
        var chain = new InterceptorChain([new ChangeFilterInterceptor(_ => 1), new SnapshotInterceptor(store)]);

        chain.Run(Live(Metric.Cpu, 30));
        Assert.Null(chain.Run(Live(Metric.Cpu, 30)));
        Assert.True(store.TryGet(Metric.Cpu, out var stored));
        Assert.Equal(30, stored.Value);
    }

    [Fact]
    public void Mapper_TimeIsMinutesSinceMidnight()
    {
        var registry = new OsEventDataRegistry();
        EventDataMapper.Apply(registry, Live(Metric.Time, (21 * 60) + 45));

        Assert.Equal(21, registry.Time!.Hour);
        Assert.Equal(45, registry.Time.Minute);
    }

    [Fact]
    public void Snapshot_ReturnsNewInstancesEveryTime()
    {
        var store = new MetricSnapshotStore();
        store.Update(Live(Metric.Battery, 40));

        var first = store.ToRegistry();
        var second = store.ToRegistry();

        Assert.Equal(40, first.Battery!.ChargePercent);
        Assert.NotSame(first.Battery, second.Battery);
        Assert.Null(first.Cpu);
    }
}
