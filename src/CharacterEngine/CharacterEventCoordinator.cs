namespace CharacterEngine;

using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Configuration;
using MonkeyStack.Core.Events;

/// <summary>
/// A Karakter állapotgép koordinátora (Monitor Object / Reactor minta).
/// A Karakter birtokolja az állapotot (OsEventDataRegistry és MovementState):
/// 1. Fogadja az OS eseményeket az IOsEventNotifier-től és frissíti a saját regiszterét.
/// 2. Lekérdezi (poll/pull) az állapotmentes IConfigurationService-től az aktuális mozgásállapothoz
///    tartozó 1 db animációt, valamint a környezeti állapotokhoz tartozó overlay animációk listáját.
/// 3. Feliratkozik a ConfigurationChanged eseményre is, hogy konfiguráció-módosításkor azonnal frissüljön.
/// </summary>
public class CharacterEventCoordinator
{
    private readonly object _syncRoot = new();
    private readonly IConfigurationService _configurationService;
    private readonly IOsEventNotifier _osEventNotifier;
    private readonly OsEventDataRegistry _registry;

    private MovementState _currentMovementState = MovementState.Falling;
    private IReadOnlyList<IAnimation> _currentOverlayAnimations = Array.Empty<IAnimation>();
    private IAnimation? _currentMovementAnimation;

    public OsEventDataRegistry Registry => _registry;

    public MovementState CurrentMovementState
    {
        get { lock (_syncRoot) { return _currentMovementState; } }
    }

    public IReadOnlyList<IAnimation> CurrentOverlayAnimations
    {
        get { lock (_syncRoot) { return _currentOverlayAnimations; } }
    }

    public IAnimation? CurrentMovementAnimation
    {
        get { lock (_syncRoot) { return _currentMovementAnimation; } }
    }

    public CharacterEventCoordinator(
        IConfigurationService configurationService,
        IOsEventNotifier osEventNotifier,
        OsEventDataRegistry? initialRegistry = null)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _osEventNotifier = osEventNotifier ?? throw new ArgumentNullException(nameof(osEventNotifier));
        _registry = initialRegistry ?? new OsEventDataRegistry();

        SubscribeToEvents();
        RefreshAllAnimations();
    }

    private void SubscribeToEvents()
    {
        _osEventNotifier.CpuChanged += (s, e) => UpdateOsMetric(() => _registry.Cpu = e);
        _osEventNotifier.MemoryChanged += (s, e) => UpdateOsMetric(() => _registry.Memory = e);
        _osEventNotifier.DiskChanged += (s, e) => UpdateOsMetric(() => _registry.Disk = e);
        _osEventNotifier.BatteryChanged += (s, e) => UpdateOsMetric(() => _registry.Battery = e);
        _osEventNotifier.WindowCountChanged += (s, e) => UpdateOsMetric(() => _registry.WindowCount = e);
        _osEventNotifier.TimeChanged += (s, e) => UpdateOsMetric(() => _registry.Time = e);
        _osEventNotifier.InactivityChanged += (s, e) => UpdateOsMetric(() => _registry.Inactivity = e);

        _configurationService.ConfigurationChanged += (s, e) => RefreshAllAnimations();
    }

    private void UpdateOsMetric(Action updateAction)
    {
        lock (_syncRoot)
        {
            updateAction();
            _currentOverlayAnimations = _configurationService.GetOverlayAnimations(_registry);
        }
    }

    private void RefreshAllAnimations()
    {
        lock (_syncRoot)
        {
            _currentOverlayAnimations = _configurationService.GetOverlayAnimations(_registry);
            _currentMovementAnimation = _configurationService.GetMovementAnimation(_currentMovementState);
        }
    }

    public IAnimation? UpdateMovementState(MovementState state)
    {
        lock (_syncRoot)
        {
            _currentMovementState = state;
            _currentMovementAnimation = _configurationService.GetMovementAnimation(state);
            return _currentMovementAnimation;
        }
    }
}
