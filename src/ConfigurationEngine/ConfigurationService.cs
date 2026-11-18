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
/// 3. Az összes Animation objektum folyamatos pörgetése (UpdateAnimations).
/// 4. Állapotmentes kiértékelés: a Karakter által lekérdezett mozgás- (1 db IAnimation)
///    és overlay animációk (ZIndex szerint rendezett IReadOnlyList&lt;IAnimation&gt;) visszaadása.
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
        set => SetCharacterSize(value, CharacterHeight);
    }

    public int CharacterHeight
    {
        get { lock (_syncRoot) { return _characterHeight; } }
        set => SetCharacterSize(CharacterWidth, value);
    }

    /// <summary>
    /// Beállítja a karakter és az összes overlay animáció egységes méretét (pl. 100x100 px),
    /// átméretezi a regisztrált animációkat, és elsüti a ConfigurationChanged eseményt.
    /// </summary>
    public void SetCharacterSize(int width, int height)
    {
        lock (_syncRoot)
        {
            if (width > 0)
            {
                _characterWidth = width;
            }

            if (height > 0)
            {
                _characterHeight = height;
            }

            foreach (var animation in _allAnimations)
            {
                animation.SetSize(_characterWidth, _characterHeight);
            }

            // TODO (Config csapat): Ha a nyers képekből újra kell skálázni a BitmapSource képkockákat,
            // itt hívható az animation.SetFrames(rescaledFrames, _characterWidth, _characterHeight).
        }

        OnConfigurationChanged();
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
            // a registry értékei alapján, majd a teljesülő szabályokhoz tartozó IAnimation objektumok
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

    /// <summary>
    /// Elsüti a ConfigurationChanged eseményt (hívandó szabály/animáció módosításakor is).
    /// </summary>
    protected virtual void OnConfigurationChanged()
    {
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
}
