using System.Windows;
using ConfigurationEngine;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Configuration;
using MonkeyStack.Core.Events;
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
    public OsEventMonitorService SharedOsMonitorService { get; }
    public IConfigurationService SharedConfigurationService { get; } = new ConfigurationService();
    public CharacterEventCoordinator SharedCoordinator { get; }
    public ICharacterPhysicsModel SharedPhysicsModel { get; } = new CharacterPhysicsModel();

    public App()
    {
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
