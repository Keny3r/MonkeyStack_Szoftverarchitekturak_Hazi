namespace CharacterEngine;

using ConfigurationEngine;
using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;
using OsEventEngine;

/// <summary>
/// Koordinálja az OS események fogadását, a központi eseménytár frissítését,
/// valamint a konfigurációs motorból lekérdezett overlay és mozgási animációk kezelését.
/// Közvetlenül használja az OsEventNotifier eseményeit és az IConfigurationService interfészt.
/// </summary>
public class CharacterEventCoordinator
{
    private readonly IConfigurationService _configurationService;
    private readonly OsEventNotifier _osEventNotifier;
    private readonly OsEventDataRegistry _registry;

    public OsEventDataRegistry Registry => _registry;
    public IAnimation? CurrentOverlayAnimation { get; private set; }
    public IAnimation? CurrentMovementAnimation { get; private set; }

    public CharacterEventCoordinator(
        IConfigurationService configurationService,
        OsEventNotifier osEventNotifier,
        OsEventDataRegistry? initialRegistry = null)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _osEventNotifier = osEventNotifier ?? throw new ArgumentNullException(nameof(osEventNotifier));
        _registry = initialRegistry ?? new OsEventDataRegistry();

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _osEventNotifier.CpuChanged += (s, e) => { _registry.Cpu = e; OnStateUpdated(); };
        _osEventNotifier.MemoryChanged += (s, e) => { _registry.Memory = e; OnStateUpdated(); };
        _osEventNotifier.DiskChanged += (s, e) => { _registry.Disk = e; OnStateUpdated(); };
        _osEventNotifier.BatteryChanged += (s, e) => { _registry.Battery = e; OnStateUpdated(); };
        _osEventNotifier.WindowCountChanged += (s, e) => { _registry.WindowCount = e; OnStateUpdated(); };
        _osEventNotifier.TimeChanged += (s, e) => { _registry.Time = e; OnStateUpdated(); };
        _osEventNotifier.InactivityChanged += (s, e) => { _registry.Inactivity = e; OnStateUpdated(); };
    }

    private void OnStateUpdated()
    {
        CurrentOverlayAnimation = _configurationService.GetOverlayAnimation(_registry);
    }

    public IAnimation? UpdateMovementState(MovementState state)
    {
        CurrentMovementAnimation = _configurationService.GetMovementAnimation(state);
        return CurrentMovementAnimation;
    }
}
