using System.Windows;
using ConfigurationEngine;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

namespace CharacterEngine;

/// <summary>
/// A Karakter (Screenmate) View ablaka.
/// Feladata a karakter alap-animációjának és a Z-sorrendű overlay animációknak a kirenderelése
/// (AnimationView.Draw(x, y, isFlipped)) a fizikai modell alsó-középső (X, Y) pozíciója alapján.
/// </summary>
public partial class MainWindow : Window
{
    public CharacterEventCoordinator Coordinator { get; }
    public ICharacterPhysicsModel Physics { get; }

    public MainWindow()
        : this(null, null)
    {
    }

    public MainWindow(CharacterEventCoordinator? coordinator, ICharacterPhysicsModel? physicsModel = null)
    {
        Coordinator = coordinator ?? new CharacterEventCoordinator(new ConfigurationService(), new OsEventNotifier());
        Physics = physicsModel ?? new CharacterPhysicsModel();
        InitializeComponent();
    }
}
