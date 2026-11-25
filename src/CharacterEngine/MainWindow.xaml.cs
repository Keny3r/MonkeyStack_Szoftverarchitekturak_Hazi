using System.Windows;
using System.Windows.Controls;
using ConfigurationEngine;
using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;
using MonkeyStack.Core.Timing;

namespace CharacterEngine;

/// <summary>
/// A Karakter (Screenmate) View ablaka.
/// Feladata a karakter alap-animációjának (1 db IAnimation) és a Z-sorrendű overlay animációknak
/// (IReadOnlyList&lt;IAnimation&gt;) egy helyre történő kirenderelése az AnimationView.Draw(x, y, isFlipped)
/// metódussal, a fizikai modell alsó-középső (X, Y) logikai pontja alapján.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Dictionary<IAnimation, AnimationView> _animationViews = new();

    public CharacterEventCoordinator Coordinator { get; }
    public ICharacterPhysicsModel Physics { get; }

    public MainWindow()
        : this(null, null)
    {
    }

    public MainWindow(CharacterEventCoordinator? coordinator, ICharacterPhysicsModel? physicsModel = null)
    {
        Coordinator = coordinator ?? new CharacterEventCoordinator(new ConfigurationService(new FrameLoop()), new OsEventNotifier());
        Physics = physicsModel ?? new CharacterPhysicsModel();
        InitializeComponent();
    }

    /// <summary>
    /// Kirajzolja a karakter aktuális mozgás-animációját és az összes aktív overlay animációt a megadott Canvas-ra.
    /// A fizikai modell alsó-középső (Physics.X, Physics.Y) talppontjából számolja ki a bal-felső (x, y) koordinátát,
    /// és ha a karakter balra néz (Physics.IsFacingLeft), mindent vízszintesen tükröz.
    /// </summary>
    public void RenderCharacter(Canvas targetCanvas)
    {
        ArgumentNullException.ThrowIfNull(targetCanvas);

        var activeAnimations = new HashSet<IAnimation>();

        // 1. Karakter mozgásállapot animációjának kirajzolása
        var movementAnimation = Coordinator.CurrentMovementAnimation;
        if (movementAnimation is not null)
        {
            activeAnimations.Add(movementAnimation);
            DrawAnimationAtBottomCenter(targetCanvas, movementAnimation, Physics.X, Physics.Y, Physics.IsFacingLeft);
        }

        // 2. Aktív overlay animációk kirajzolása (Z-index sorrendben)
        foreach (var overlayAnimation in Coordinator.CurrentOverlayAnimations)
        {
            activeAnimations.Add(overlayAnimation);
            DrawAnimationAtBottomCenter(targetCanvas, overlayAnimation, Physics.X, Physics.Y, Physics.IsFacingLeft);
        }

        // 3. Már nem aktív animációk elrejtése
        foreach (var (animation, view) in _animationViews)
        {
            if (!activeAnimations.Contains(animation))
            {
                view.Hide();
            }
        }
    }

    private void DrawAnimationAtBottomCenter(
        Canvas targetCanvas,
        IAnimation animation,
        double bottomCenterX,
        double bottomCenterY,
        bool isFlipped)
    {
        if (!_animationViews.TryGetValue(animation, out var view))
        {
            view = new AnimationView(animation, targetCanvas);
            _animationViews[animation] = view;
        }

        double topLeftX = bottomCenterX - (animation.GetWidth() / 2.0);
        double topLeftY = bottomCenterY - animation.GetHeight();

        view.Draw(topLeftX, topLeftY, isFlipped);
    }
}
