namespace CharacterEngine.Rendering;

using System.Windows.Controls;
using System.Windows.Media;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Timing;

/// <summary>
/// A szimulációs ciklus és a WPF UI-szálas renderelés összekötője
/// (Half Sync / Half Async és Monitor Object minta).
/// 
/// Aszinkron réteg:
/// - A háttérben futó léptető (<see cref="IFrameLoop"/>) tickjeire vagy kézi hívásra (<see cref="StepSimulation"/>)
///   összegyűjti az aktuális platformokat és egérbemenetet, lépteti a fizikai modellt (<see cref="ICharacterPhysicsModel"/>),
///   állapotváltáskor frissíti a koordinátort (<see cref="CharacterEventCoordinator"/>),
///   és egy szálbiztos <see cref="CharacterRenderSnapshot"/> pillanatképet állít elő.
/// 
/// Szinkron réteg:
/// - A WPF UI szál renderelési ciklusában (<see cref="CompositionTarget.Rendering"/>) vagy explicit
///   hívásra (<see cref="Render"/>) lekéri a legfrissebb pillanatképet, és átadja az
///   <see cref="ICharacterRenderer"/>-nek kirajzolásra.
/// </summary>
public class RenderManager : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly ICharacterPhysicsModel _physics;
    private readonly CharacterEventCoordinator _coordinator;
    private readonly ICharacterRenderer _renderer;
    private readonly IPlatformProvider? _platformProvider;
    private readonly Func<(double cursorX, double cursorY, bool isMouseDown)>? _inputProvider;

    private IFrameLoop? _frameLoop;
    private Canvas? _attachedCanvas;
    private CharacterRenderSnapshot _latestSnapshot = CharacterRenderSnapshot.Empty;
    private bool _isVisible = true;
    private bool _disposed;

    /// <summary>
    /// A legutóbb előállított, szálbiztosan lekérhető pillanatkép.
    /// </summary>
    public CharacterRenderSnapshot LatestSnapshot
    {
        get
        {
            lock (_syncRoot)
            {
                return _latestSnapshot;
            }
        }
    }

    /// <summary>
    /// A szimulált fizikai modell.
    /// </summary>
    public ICharacterPhysicsModel Physics => _physics;

    /// <summary>
    /// A karakter eseménykoordinátora.
    /// </summary>
    public CharacterEventCoordinator Coordinator => _coordinator;

    /// <summary>
    /// A karakter renderelője.
    /// </summary>
    public ICharacterRenderer Renderer => _renderer;

    /// <summary>
    /// Megjelenítendő-e a karakter a képernyőn.
    /// </summary>
    public bool IsVisible
    {
        get
        {
            lock (_syncRoot)
            {
                return _isVisible;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _isVisible = value;
                _latestSnapshot = _latestSnapshot with { IsVisible = value };
            }
        }
    }

    /// <summary>
    /// Esemény, amely minden új pillanatkép előállításakor kiváltódik (a szimuláció szálán).
    /// </summary>
    public event EventHandler<CharacterRenderSnapshot>? SnapshotUpdated;

    public RenderManager(
        ICharacterPhysicsModel physics,
        CharacterEventCoordinator coordinator,
        ICharacterRenderer? renderer = null,
        IPlatformProvider? platformProvider = null,
        Func<(double cursorX, double cursorY, bool isMouseDown)>? inputProvider = null)
    {
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _renderer = renderer ?? new CharacterRenderer();
        _platformProvider = platformProvider;
        _inputProvider = inputProvider;

        // Kezdeti snapshot előállítása
        PublishInitialSnapshot();
    }

    private void PublishInitialSnapshot()
    {
        lock (_syncRoot)
        {
            _latestSnapshot = new CharacterRenderSnapshot(
                X: _physics.X,
                Y: _physics.Y,
                IsFacingLeft: _physics.IsFacingLeft,
                MovementAnimation: _coordinator.CurrentMovementAnimation,
                OverlayAnimations: _coordinator.CurrentOverlayAnimations,
                IsVisible: _isVisible);
        }
    }

    /// <summary>
    /// Végrehajt egyetlen szimulációs lépést a megadott vagy automatikusan szolgáltatott bemenetekkel.
    /// Bármelyik szálról biztonságosan hívható (Monitor Object minta).
    /// </summary>
    /// <param name="deltaTime">Az előző lépés óta eltelt idő.</param>
    /// <param name="overridePlatforms">Opcionális platform-lista (ha null, az IPlatformProvider-ből kérdezi le).</param>
    /// <param name="overrideCursorX">Opcionális kurzor X koordináta.</param>
    /// <param name="overrideCursorY">Opcionális kurzor Y koordináta.</param>
    /// <param name="overrideIsMouseDown">Opcionális egérgomb állapot.</param>
    public CharacterRenderSnapshot StepSimulation(
        TimeSpan deltaTime,
        IReadOnlyList<PlatformSegment>? overridePlatforms = null,
        double? overrideCursorX = null,
        double? overrideCursorY = null,
        bool? overrideIsMouseDown = null)
    {
        CharacterRenderSnapshot snapshot;
        EventHandler<CharacterRenderSnapshot>? handler;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return _latestSnapshot;
            }

            // 1. Platformok összegyűjtése
            var platforms = overridePlatforms
                ?? _platformProvider?.Current.Platforms
                ?? Array.Empty<PlatformSegment>();

            // 2. Egérbemenet összegyűjtése
            double cursorX;
            double cursorY;
            bool isMouseDown;

            if (overrideCursorX.HasValue && overrideCursorY.HasValue && overrideIsMouseDown.HasValue)
            {
                cursorX = overrideCursorX.Value;
                cursorY = overrideCursorY.Value;
                isMouseDown = overrideIsMouseDown.Value;
            }
            else if (_inputProvider is not null)
            {
                var input = _inputProvider();
                cursorX = overrideCursorX ?? input.cursorX;
                cursorY = overrideCursorY ?? input.cursorY;
                isMouseDown = overrideIsMouseDown ?? input.isMouseDown;
            }
            else
            {
                cursorX = overrideCursorX ?? 0.0;
                cursorY = overrideCursorY ?? 0.0;
                isMouseDown = overrideIsMouseDown ?? false;
            }

            // 3. Karakter méretének meghatározása az aktuális animációból
            var movementAnim = _coordinator.CurrentMovementAnimation;
            int width = movementAnim?.GetWidth() ?? 64;
            int height = movementAnim?.GetHeight() ?? 64;
            if (width <= 0)
            {
                width = 64;
            }

            if (height <= 0)
            {
                height = 64;
            }

            // 4. Fizikai modell léptetése
            _physics.Update(deltaTime, platforms, cursorX, cursorY, isMouseDown, width, height);

            // 5. Ha a fizikai mozgásállapot változott, frissítjük a koordinátort
            if (_physics.State != _coordinator.CurrentMovementState)
            {
                _coordinator.UpdateMovementState(_physics.State);
            }

            // 6. Új szálbiztos pillanatkép létrehozása
            snapshot = new CharacterRenderSnapshot(
                X: _physics.X,
                Y: _physics.Y,
                IsFacingLeft: _physics.IsFacingLeft,
                MovementAnimation: _coordinator.CurrentMovementAnimation,
                OverlayAnimations: _coordinator.CurrentOverlayAnimations,
                IsVisible: _isVisible);

            _latestSnapshot = snapshot;
            handler = SnapshotUpdated;
        }

        handler?.Invoke(this, snapshot);
        return snapshot;
    }

    /// <summary>
    /// Kirajzolja a legfrissebb pillanatképet a cél Canvas-ra.
    /// Ezt a metódust a WPF UI szálon kell hívni!
    /// </summary>
    public void Render(Canvas targetCanvas)
    {
        ArgumentNullException.ThrowIfNull(targetCanvas);

        var snapshot = LatestSnapshot;
        _renderer.Render(targetCanvas, snapshot);
    }

    /// <summary>
    /// Feliratkozik a megadott <see cref="IFrameLoop"/> Tick eseményére a szimuláció automatikus léptetéséhez.
    /// </summary>
    public void AttachToFrameLoop(IFrameLoop frameLoop)
    {
        ArgumentNullException.ThrowIfNull(frameLoop);

        lock (_syncRoot)
        {
            if (ReferenceEquals(_frameLoop, frameLoop))
            {
                return;
            }

            DetachFromFrameLoop();
            _frameLoop = frameLoop;
            _frameLoop.Tick += OnFrameTick;
        }
    }

    /// <summary>
    /// Leiratkozik az aktív <see cref="IFrameLoop"/>-ról.
    /// </summary>
    public void DetachFromFrameLoop()
    {
        lock (_syncRoot)
        {
            if (_frameLoop is not null)
            {
                _frameLoop.Tick -= OnFrameTick;
                _frameLoop = null;
            }
        }
    }

    private void OnFrameTick(object? sender, FrameTickEventArgs e)
    {
        StepSimulation(e.DeltaTime);
    }

    /// <summary>
    /// Összeköti a renderelést a WPF UI szál rajzolási ciklusával (<see cref="CompositionTarget.Rendering"/>).
    /// Csak a WPF UI szálról hívható!
    /// </summary>
    public void AttachToRendering(Canvas targetCanvas)
    {
        ArgumentNullException.ThrowIfNull(targetCanvas);

        DetachFromRendering();
        _attachedCanvas = targetCanvas;
        CompositionTarget.Rendering += OnCompositionTargetRendering;
    }

    /// <summary>
    /// Leválasztja a renderelőt a WPF UI szál rajzolási ciklusáról.
    /// </summary>
    public void DetachFromRendering()
    {
        if (_attachedCanvas is not null)
        {
            CompositionTarget.Rendering -= OnCompositionTargetRendering;
            _attachedCanvas = null;
        }
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        if (_attachedCanvas is not null)
        {
            Render(_attachedCanvas);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        DetachFromFrameLoop();
        DetachFromRendering();
        _renderer.DetachAll();
    }
}
