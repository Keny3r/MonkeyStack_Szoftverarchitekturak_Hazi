namespace ConfigurationEngine;

using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Configuration;
using MonkeyStack.Core.Events;

/// <summary>
/// A Konfigurációs motor Modell rétegének konkrét váza (Component Configurator / Monitor Object minta).
/// Feladata:
/// 1. Viselkedési szabályok és animációs beállítások betöltése/mentése (SQLite).
/// 2. A képek fix méretűre (CharacterWidth x CharacterHeight) skálázása és Animation objektumok létrehozása.
/// 3. Az összes Animation objektum folyamatos pörgetése (Update).
/// 4. Állapotmentes kiértékelés: a Karakter által lekérdezett mozgás- és overlay animációk visszaadása.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<MovementState, IAnimation> _movementAnimations = new();
    private readonly List<IAnimation> _allAnimations = new();
    private int _characterWidth = 100;
    private int _characterHeight = 100;

    public event EventHandler? ConfigurationChanged;

    public int CharacterWidth
    {
        get { lock (_syncRoot) { return _characterWidth; } }
        set
        {
            lock (_syncRoot)
            {
                _characterWidth = value > 0 ? value : 100;
            }
            OnConfigurationChanged();
        }
    }

    public int CharacterHeight
    {
        get { lock (_syncRoot) { return _characterHeight; } }
        set
        {
            lock (_syncRoot)
            {
                _characterHeight = value > 0 ? value : 100;
            }
            OnConfigurationChanged();
        }
    }

    /// <summary>
    /// Visszaadja az adott rendszeresemény-állapotokhoz (OsEventDataRegistry) illeszkedő
    /// overlay animációk ZIndex szerint növekvő sorrendbe rendezett listáját.
    /// </summary>
    public IReadOnlyList<IAnimation> GetOverlayAnimations(OsEventDataRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        lock (_syncRoot)
        {
            // TODO (Config csapat): A betöltött viselkedési szabályok (triggerfeltételek) kiértékelése
            // a registry értékei alapján, majd a teljesülő szabályokhoz tartozó animációk
            // ZIndex szerint rendezett (.OrderBy(a => a.ZIndex).ToList()) visszaadása.
            return Array.Empty<IAnimation>();
        }
    }

    /// <summary>
    /// Visszaadja a megadott mozgásállapothoz konfigurált 1 db animációt.
    /// </summary>
    public IAnimation? GetMovementAnimation(MovementState state)
    {
        lock (_syncRoot)
        {
            return _movementAnimations.TryGetValue(state, out var animation) ? animation : null;
        }
    }

    /// <summary>
    /// Lépteti az összes regisztrált animációt (a Config Engine időzítője hívja).
    /// </summary>
    public void UpdateAnimations(TimeSpan deltaTime)
    {
        lock (_syncRoot)
        {
            foreach (var animation in _allAnimations)
            {
                animation.Update(deltaTime);
            }
        }
    }

    protected virtual void OnConfigurationChanged()
    {
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
}
