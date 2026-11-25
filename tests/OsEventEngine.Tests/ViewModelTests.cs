namespace OsEventEngine.Tests;

using Microsoft.Extensions.Time.Testing;
using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;
using OsEventEngine.ViewModels;

/// <summary>
/// Az OS Mock View sorainak szinkronja a szimulációs modellel (natív hívás és ablak nélkül).
/// </summary>
public class ViewModelTests
{
    private static OsEventMonitorService CreateService() =>
        new(new OsEventNotifier(), OsEventEngineOptions.Default, new FakeTimeProvider(), _ => [new FakeSource(Metric.Cpu, 20)]);

    [Fact]
    public void PresetValueMovesTheSliderOnRefresh()
    {
        using var service = CreateService();
        var row = new MetricRowViewModel(MetricCatalog.Descriptor(Metric.Cpu), service);

        service.Simulation.ApplyPreset(new Dictionary<Metric, int> { [Metric.Cpu] = 95 });
        row.Refresh();

        Assert.True(row.IsSimulated);
        Assert.Equal(95, row.SliderValue);
        Assert.Equal("95 %", row.SliderText);
    }

    [Fact]
    public void DraggedSliderKeepsItsValueOnRefresh()
    {
        using var service = CreateService();
        var row = new MetricRowViewModel(MetricCatalog.Descriptor(Metric.Cpu), service);

        row.SliderValue = 73;
        row.Refresh();

        Assert.True(row.IsSimulated);
        Assert.Equal(73, row.SliderValue);
        Assert.Equal(73, service.SimulationController.GetSimulatedValue(Metric.Cpu));
    }
}
