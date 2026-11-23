namespace OsEventEngine.ViewModels;

using System.Windows.Input;
using System.Windows.Threading;
using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;

/// <summary>
/// Az OS Mock View ViewModelje (MVVM). Az értékeket 500 ms-onként a modellből frissíti (nem minden eseményre),
/// a csúszkák és a presetek a <see cref="OsEventMonitorService.Simulation"/>-t hívják, sosem közvetlenül a
/// <c>Notify…Changed</c>-et (plan.md 5.1–5.2).
/// </summary>
internal sealed class OsMockViewModel : ObservableObject, IDisposable
{
    private readonly OsEventMonitorService _service;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private string _statusText = string.Empty;
    private string _lastEventText = "Utolsó esemény: –";

    public OsMockViewModel(OsEventMonitorService service, Dispatcher dispatcher)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Rows = MetricCatalog.Descriptors.Select(d => new MetricRowViewModel(d, service)).ToList();

        OverheatCommand = Preset(new() { [Metric.Cpu] = 95, [Metric.Memory] = 92 });
        LowBatteryCommand = Preset(new() { [Metric.Battery] = 5 });
        NightCommand = Preset(new() { [Metric.Time] = (22 * 60) + 30 });
        BoredCommand = Preset(new() { [Metric.Inactivity] = 30 });
        ResetAllCommand = new RelayCommand(() =>
        {
            _service.Simulation.ResetAll();
            Refresh();
        });

        Subscribe();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => Refresh(), dispatcher);
        Refresh();
    }

    public IReadOnlyList<MetricRowViewModel> Rows { get; }

    public ICommand OverheatCommand { get; }

    public ICommand LowBatteryCommand { get; }

    public ICommand NightCommand { get; }

    public ICommand BoredCommand { get; }

    public ICommand ResetAllCommand { get; }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string LastEventText
    {
        get => _lastEventText;
        private set => SetProperty(ref _lastEventText, value);
    }

    public void Refresh()
    {
        StatusText = _service.IsRunning
            ? (_service.IsMockModeEnabled ? "Fut · szimuláció aktív" : "Fut · minden érték élő")
            : "Nem fut (a Start() még nem lett meghívva)";
        foreach (var row in Rows)
        {
            row.Refresh();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        Unsubscribe();
    }

    private RelayCommand Preset(Dictionary<Metric, int> values) => new(() =>
    {
        _service.Simulation.ApplyPreset(values);
        Refresh();
    });

    private void ShowLastEvent(string name, string value) =>
        _dispatcher.BeginInvoke(() => LastEventText = $"Utolsó esemény: {name} = {value}  ({DateTime.Now:HH:mm:ss.fff})");

    private void OnCpu(object? s, CpuEventData e) => ShowLastEvent("CpuChanged", $"{e.UsagePercent} %");
    private void OnMemory(object? s, MemoryEventData e) => ShowLastEvent("MemoryChanged", $"{e.UsagePercent} %");
    private void OnDisk(object? s, DiskEventData e) => ShowLastEvent("DiskChanged", $"{e.UsagePercent} %");
    private void OnBattery(object? s, BatteryEventData e) => ShowLastEvent("BatteryChanged", $"{e.ChargePercent} %");
    private void OnWindowCount(object? s, WindowCountEventData e) => ShowLastEvent("WindowCountChanged", $"{e.WindowCount}");
    private void OnTime(object? s, TimeEventData e) => ShowLastEvent("TimeChanged", $"{e.Hour:00}:{e.Minute:00}");
    private void OnInactivity(object? s, InactivityEventData e) => ShowLastEvent("InactivityChanged", $"{e.InactivityMinutes} perc");

    private void Subscribe()
    {
        var n = _service.Notifier;
        n.CpuChanged += OnCpu;
        n.MemoryChanged += OnMemory;
        n.DiskChanged += OnDisk;
        n.BatteryChanged += OnBattery;
        n.WindowCountChanged += OnWindowCount;
        n.TimeChanged += OnTime;
        n.InactivityChanged += OnInactivity;
    }

    private void Unsubscribe()
    {
        var n = _service.Notifier;
        n.CpuChanged -= OnCpu;
        n.MemoryChanged -= OnMemory;
        n.DiskChanged -= OnDisk;
        n.BatteryChanged -= OnBattery;
        n.WindowCountChanged -= OnWindowCount;
        n.TimeChanged -= OnTime;
        n.InactivityChanged -= OnInactivity;
    }
}
