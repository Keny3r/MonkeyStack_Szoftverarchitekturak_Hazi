using System.Windows;
using MonkeyStack.Core.Events;

namespace OsEventEngine;

/// <summary>
/// Az OS eseménymotor megjelenítő és tesztelő (Mock) View ablaka.
/// Indítható önállóan (standalone F5 teszt módban saját OsEventMonitorService példánnyal),
/// vagy integráltan a közös memóriában élő OsEventMonitorService / OsEventNotifier átadásával.
/// </summary>
public partial class MainWindow : Window
{
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
    }
}
