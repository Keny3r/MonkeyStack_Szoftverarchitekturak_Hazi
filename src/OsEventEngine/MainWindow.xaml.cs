using System.Windows;
using MonkeyStack.Core.Events;
using OsEventEngine.ViewModels;

namespace OsEventEngine;

/// <summary>
/// Az OS eseménymotor megjelenítő és tesztelő (Mock) View ablaka.
/// Indítható önállóan (standalone F5 teszt módban saját OsEventMonitorService példánnyal),
/// vagy integráltan a közös memóriában élő OsEventMonitorService / OsEventNotifier átadásával.
/// </summary>
/// <remarks>
/// MVVM: a logika az <see cref="OsMockViewModel"/>-ben van; a csúszkák a <c>Simulation</c>-t hívják,
/// így a szimulált érték ugyanazon a láncon megy ki, mint az élő (plan.md 5.2).
/// </remarks>
public partial class MainWindow : Window
{
    private readonly OsMockViewModel _viewModel;

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

        _viewModel = new OsMockViewModel(MonitorService, Dispatcher);
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }
}
