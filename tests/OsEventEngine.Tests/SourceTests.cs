namespace OsEventEngine.Tests;

using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;
using OsEventEngine.Native;
using OsEventEngine.Simulation;
using OsEventEngine.Sources;

public class SourceTests
{
    private const long Ms = TimeSpan.TicksPerMillisecond;

    [Fact]
    public void Cpu_FirstSampleIsNull()
    {
        var cpu = new CpuSource(() => new SystemTimes(0, 0, 0), processorCount: 1);

        Assert.Null(cpu.Sample());
    }

    [Fact]
    public void Cpu_ComputesBusyShareFromDeltas()
    {
        // 1 mag, 1000 ms: kernel 600 ms (ebből 500 ms üresjárat) + user 400 ms → 50% foglalt.
        var times = new Queue<SystemTimes>([new(0, 0, 0), new(500 * Ms, 600 * Ms, 400 * Ms)]);
        var cpu = new CpuSource(times.Dequeue, processorCount: 1);

        cpu.Sample();
        Assert.Equal(50, cpu.Sample());
    }

    [Fact]
    public void Cpu_TooShortWindowKeepsBaseline()
    {
        var times = new Queue<SystemTimes>([
            new(0, 0, 0),
            new(10 * Ms, 50 * Ms, 0),         // 50 ms: túl rövid, nincs érték
            new(250 * Ms, 300 * Ms, 200 * Ms) // 500 ms az első minta óta: 50% foglalt
        ]);
        var cpu = new CpuSource(times.Dequeue, processorCount: 1);

        cpu.Sample();
        Assert.Null(cpu.Sample());
        Assert.Equal(50, cpu.Sample());
    }

    [Fact]
    public void Disk_ComputesUsedShareAndHandlesEmptyDrive()
    {
        Assert.Equal(75, new DiskSource(() => (400, 100)).Sample());
        Assert.Null(new DiskSource(() => (0, 0)).Sample());
    }

    [Fact]
    public void Inactivity_ConvertsMillisecondsToWholeMinutes()
    {
        Assert.Equal(0, new InactivitySource(() => 59_999).Sample());
        Assert.Equal(3, new InactivitySource(() => 180_500).Sample());
    }

    [Fact]
    public void Switchable_SimulatedModeDoesNotCallLiveSource()
    {
        var live = new FakeSource(Metric.Cpu, 20);
        var source = new SwitchableSource(live, MetricCatalog.Descriptor(Metric.Cpu));
        source.Sample();

        source.Mode = SourceMode.Simulated;
        var (value, origin) = source.SampleWithOrigin();
        source.Sample();

        Assert.Equal(1, live.Calls);
        Assert.Equal(20, value);
        Assert.Equal(SampleOrigin.Simulated, origin);
    }

    [Fact]
    public void Switchable_SimulatedValueIsClampedToDescriptor()
    {
        var source = new SwitchableSource(new FakeSource(Metric.WindowCount, 3), MetricCatalog.Descriptor(Metric.WindowCount));

        source.SimulatedValue = 500;

        Assert.Equal(MetricCatalog.Descriptor(Metric.WindowCount).Max, source.SimulatedValue);
    }

    [Fact]
    public void Simulation_SetSimulatedValueOverridesLastLiveValue()
    {
        var source = new SwitchableSource(new FakeSource(Metric.Cpu, 20), MetricCatalog.Descriptor(Metric.Cpu));
        source.Sample();
        var controller = new SimulationController(new Dictionary<Metric, SwitchableSource> { [Metric.Cpu] = source }, _ => null);

        controller.SetSimulatedValue(Metric.Cpu, 95);

        Assert.Equal(SourceMode.Simulated, controller.GetMode(Metric.Cpu));
        Assert.Equal((95, SampleOrigin.Simulated), source.SampleWithOrigin());
    }

    [Fact]
    public void Simulation_ResetAllReturnsToLive()
    {
        var source = new SwitchableSource(new FakeSource(Metric.Cpu, 20), MetricCatalog.Descriptor(Metric.Cpu));
        var controller = new SimulationController(new Dictionary<Metric, SwitchableSource> { [Metric.Cpu] = source }, _ => null);
        controller.SetSimulatedValue(Metric.Cpu, 95);

        controller.ResetAll();

        Assert.Equal((20, SampleOrigin.Live), source.SampleWithOrigin());
    }
}
