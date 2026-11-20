using System.Text;
using System.Windows;
using System.Windows.Threading;
using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;

namespace OsEventEngine;

/// <summary>
/// Az OS eseménymotor megjelenítő és tesztelő (Mock) View ablaka.
/// Indítható önállóan (standalone F5 teszt módban saját OsEventMonitorService példánnyal),
/// vagy integráltan a közös memóriában élő OsEventMonitorService / OsEventNotifier átadásával.
/// </summary>
/// <remarks>
/// Jelenleg csak olvasható állapotkijelzés: az értékeket 500 ms-onként a <see cref="OsEventMonitorService.GetSnapshot"/>-ból
/// frissíti, nem minden eseményre. A csúszkás, MVVM-alapú felület az M6 lépésben jön (plan.md 5.2).
/// </remarks>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _refreshTimer;

    public OsEventMonitorService MonitorService { get; }
    public OsEventNotifier Notifier => MonitorService.Notifier;

    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(OsEventMonitorService? monitorService)
    {
        MonitorService = monitorService ?? new OsEventMonitorService();
        InitializeComponent();

        SubscribeLastEvent();
        _refreshTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher);
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            UnsubscribeLastEvent();
        };
        Refresh();
    }

    private void Refresh()
    {
        var snapshot = MonitorService.GetSnapshot();
        var text = new StringBuilder();
        text.AppendLine(MonitorService.IsRunning ? "Állapot: fut" : "Állapot: nem fut (Start() még nem hívták)");
        text.AppendLine();
        foreach (var descriptor in MetricCatalog.Descriptors)
        {
            string value = descriptor.Metric switch
            {
                Metric.Cpu => Format(snapshot.Cpu?.UsagePercent, "%"),
                Metric.Memory => Format(snapshot.Memory?.UsagePercent, "%"),
                Metric.Disk => Format(snapshot.Disk?.UsagePercent, "%"),
                Metric.Battery => Format(snapshot.Battery?.ChargePercent, "%"),
                Metric.WindowCount => Format(snapshot.WindowCount?.WindowCount, "db"),
                Metric.Time => snapshot.Time is { } t ? $"{t.Hour:00}:{t.Minute:00}" : "–",
                Metric.Inactivity => Format(snapshot.Inactivity?.InactivityMinutes, "perc"),
                _ => "?",
            };

            text.AppendLine($"{descriptor.DisplayName,-12} {value,-10} {MonitorService.GetMonitorInfo(descriptor.Metric)}");
        }

        StatusText.Text = text.ToString();
    }

    private static string Format(int? value, string unit) => value is int v ? $"{v} {unit}" : "–";

    private void ShowLastEvent(string name, string value) =>
        Dispatcher.BeginInvoke(() => LastEventText.Text = $"Utolsó esemény: {name} = {value}  ({DateTime.Now:HH:mm:ss.fff})");

    private void OnCpu(object? s, CpuEventData e) => ShowLastEvent("CpuChanged", $"{e.UsagePercent} %");
    private void OnMemory(object? s, MemoryEventData e) => ShowLastEvent("MemoryChanged", $"{e.UsagePercent} %");
    private void OnDisk(object? s, DiskEventData e) => ShowLastEvent("DiskChanged", $"{e.UsagePercent} %");
    private void OnBattery(object? s, BatteryEventData e) => ShowLastEvent("BatteryChanged", $"{e.ChargePercent} %");
    private void OnWindowCount(object? s, WindowCountEventData e) => ShowLastEvent("WindowCountChanged", $"{e.WindowCount}");
    private void OnTime(object? s, TimeEventData e) => ShowLastEvent("TimeChanged", $"{e.Hour:00}:{e.Minute:00}");
    private void OnInactivity(object? s, InactivityEventData e) => ShowLastEvent("InactivityChanged", $"{e.InactivityMinutes} perc");

    private void SubscribeLastEvent()
    {
        Notifier.CpuChanged += OnCpu;
        Notifier.MemoryChanged += OnMemory;
        Notifier.DiskChanged += OnDisk;
        Notifier.BatteryChanged += OnBattery;
        Notifier.WindowCountChanged += OnWindowCount;
        Notifier.TimeChanged += OnTime;
        Notifier.InactivityChanged += OnInactivity;
    }

    private void UnsubscribeLastEvent()
    {
        Notifier.CpuChanged -= OnCpu;
        Notifier.MemoryChanged -= OnMemory;
        Notifier.DiskChanged -= OnDisk;
        Notifier.BatteryChanged -= OnBattery;
        Notifier.WindowCountChanged -= OnWindowCount;
        Notifier.TimeChanged -= OnTime;
        Notifier.InactivityChanged -= OnInactivity;
    }
}
