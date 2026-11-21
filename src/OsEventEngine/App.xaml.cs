using System.Windows;

namespace OsEventEngine;

/// <summary>
/// Önálló (standalone F5) indítás: saját <see cref="OsEventMonitorService"/>, naplózással, és az OS Mock View.
/// Integrált futásnál ez az osztály nem fut; ott a <c>CharacterEngine</c> a host (plan.md 5.3).
/// </summary>
public partial class App : Application
{
    private OsEventMonitorService? _service;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _service = new OsEventMonitorService(options: OsEventEngineOptions.Default with { EnableLogging = true });
        var window = new MainWindow(_service);
        window.Show();

        // Előbb feliratkozás (a MainWindow konstruktorában), utána indítás, hogy a kezdeti pillanatkép is megérkezzen.
        _service.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _service?.Dispose();
        base.OnExit(e);
    }
}
