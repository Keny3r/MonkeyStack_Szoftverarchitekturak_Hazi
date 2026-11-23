using System.Windows;
using ConfigurationEngine;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Configuration;
using MonkeyStack.Core.Events;
using MonkeyStack.Core.Timing;
using OsEventEngine;

namespace CharacterEngine;

/// <summary>
/// A közös memóriában futó alkalmazás összekapcsolási pontja (Component Configurator minta).
/// Egyetlen közös memóriatérben példányosítja a Modell réteg objektumait (OsEventNotifier,
/// ConfigurationService, CharacterEventCoordinator, CharacterPhysicsModel), így minden ablak
/// (Karakter View, Config View, OS Mock View) ugyanazokon az élő objektumpéldányokon osztozik.
/// </summary>
public partial class App : Application
{
    public OsEventNotifier SharedOsEventNotifier { get; } = new();

    /// <summary>A közös léptető: ennek ütemére lépnek az animációk (és később a Karakter fizikája is).</summary>
    public FrameLoop SharedFrameLoop { get; } = new();

    public OsEventMonitorService SharedOsMonitorService { get; }
    public IConfigurationService SharedConfigurationService { get; }
    public CharacterEventCoordinator SharedCoordinator { get; }
    public ICharacterPhysicsModel SharedPhysicsModel { get; } = new CharacterPhysicsModel();

    public App()
    {
        SharedConfigurationService = new ConfigurationService(SharedFrameLoop);
        SharedFrameLoop.Start();
        SharedOsMonitorService = new OsEventMonitorService(SharedOsEventNotifier);
        SharedCoordinator = new CharacterEventCoordinator(SharedConfigurationService, SharedOsEventNotifier);
    }

    /// <summary>
    /// Megnyitja a Konfigurációs ablakot a közös memóriabeli IConfigurationService példánnyal.
    /// </summary>
    public ConfigurationEngine.MainWindow OpenConfigurationWindow()
    {
        var window = new ConfigurationEngine.MainWindow(SharedConfigurationService);
        window.Show();
        return window;
    }

    /// <summary>
    /// Megnyitja az OS Esemény (és Mock tesztelő) ablakot a közös memóriabeli OsEventMonitorService példánnyal.
    /// </summary>
    public OsEventEngine.MainWindow OpenOsEventWindow()
    {
        var window = new OsEventEngine.MainWindow(SharedOsMonitorService);
        window.Show();
        return window;
    }
}
