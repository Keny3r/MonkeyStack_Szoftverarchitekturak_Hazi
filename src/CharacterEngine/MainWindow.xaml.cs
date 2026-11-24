using System.Windows;
using System.Windows.Controls;
using CharacterEngine.Rendering;
using ConfigurationEngine;
using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

namespace CharacterEngine;

/// <summary>
/// A Karakter (Screenmate) View ablaka.
/// Feladata a karakter és overlay-ei megjelenítésének összefogása, a renderelést az <see cref="ICharacterRenderer"/> végzi
/// a fizikai modell alsó-középső (X, Y) logikai pontja alapján.
/// </summary>
public partial class MainWindow : Window
{
    public CharacterEventCoordinator Coordinator { get; }
    public ICharacterPhysicsModel Physics { get; }
    public ICharacterRenderer Renderer { get; }

    public MainWindow()
        : this(null, null, null)
    {
    }

    public MainWindow(
        CharacterEventCoordinator? coordinator,
        ICharacterPhysicsModel? physicsModel = null,
        ICharacterRenderer? renderer = null)
    {
        Coordinator = coordinator ?? new CharacterEventCoordinator(new ConfigurationService(), new OsEventNotifier());
        Physics = physicsModel ?? new CharacterPhysicsModel();
        Renderer = renderer ?? new CharacterRenderer();
        InitializeComponent();
    }

    /// <summary>
    /// Kirajzolja a karakter aktuális mozgás-animációját és az összes aktív overlay animációt a megadott Canvas-ra.
    /// </summary>
    public void RenderCharacter(Canvas targetCanvas)
    {
        ArgumentNullException.ThrowIfNull(targetCanvas);

        var snapshot = new CharacterRenderSnapshot(
            Physics.X,
            Physics.Y,
            Physics.IsFacingLeft,
            Coordinator.CurrentMovementAnimation,
            Coordinator.CurrentOverlayAnimations);

        Renderer.Render(targetCanvas, snapshot);
    }
}
