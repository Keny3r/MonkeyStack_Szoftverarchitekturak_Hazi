namespace ConfigurationEngine;

using System.Diagnostics;
using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using ConfigurationEngine.Rules;
using ConfigurationEngine.Runtime;
using ConfigurationEngine.Storage;
using ConfigurationEngine.Validation;
using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Configuration;
using MonkeyStack.Core.Events;

/// <summary>
/// A Konfigurációs motor Modell rétege (Component Configurator / Monitor Object minta).
/// Feladata:
/// 1. A konfiguráció betöltése a tárolóból, és újratöltése minden sikeres mentés után (<see cref="IConfigurationStore.Changed"/>).
/// 2. A képek fix méretűre (CharacterWidth x CharacterHeight) skálázása és Animation objektumok létrehozása.
/// 3. Az összes Animation objektum folyamatos pörgetése (UpdateAnimations).
/// 4. Állapotmentes kiértékelés: a Karakter által lekérdezett mozgás- (1 db IAnimation)
///    és overlay animációk (ZIndex szerint rendezett IReadOnlyList&lt;IAnimation&gt;) visszaadása.
/// Leírás: docs/configuration_engine/service.md.
/// </summary>
public sealed class ConfigurationService : IConfigurationService, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly IConfigurationStore _store;
    private readonly AnimationSetBuilder _builder;

    /// <summary>Sorosítja az újratöltést és az újraskálázást; a lassú képbetöltés e mögött, a <see cref="_syncRoot"/>-on kívül fut.</summary>
    private readonly SemaphoreSlim _rebuildGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();

    private AnimationSet _current = AnimationSet.Empty;
    private int _characterWidth = ConfigurationDefaults.CreateCharacterSettings().CharacterWidth;
    private int _characterHeight = ConfigurationDefaults.CreateCharacterSettings().CharacterHeight;

    /// <summary>Minden méretváltozáskor nő; a korábbi méretre készült, közben elavult újraskálázás így eldobható.</summary>
    private int _sizeVersion;
    private bool _disposed;

    /// <summary>
    /// A felhasználó adatbázisával (<see cref="SqliteConfigurationStore.DefaultDatabasePath"/>) és a szállított képekkel.
    /// Nem végez I/O-t: a konfiguráció az <see cref="InitializeAsync"/> hívásakor töltődik be.
    /// </summary>
    public ConfigurationService()
        : this(new SqliteConfigurationStore(mediaValidator: new MediaValidator()), new FrameLoader())
    {
    }

    public ConfigurationService(IConfigurationStore store, IFrameLoader frameLoader)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(frameLoader);

        _store = store;
        _builder = new AnimationSetBuilder(frameLoader);
        _store.Changed += OnStoreChanged;
    }

    /// <summary>
    /// Tüzel, ha a futó konfiguráció megváltozott (újratöltés, méretváltozás); a Karakter ekkor újra lekérdezi az
    /// animációkat. Bármelyik szálon kiváltódhat, és sosem a motor zárja alatt.
    /// </summary>
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
    /// A legutóbbi felépítéskor be nem töltött animációs források. A rájuk hivatkozó mozgásállapot a szállított
    /// alapanimációt kapta, a rájuk hivatkozó szabályok nem jelennek meg.
    /// </summary>
    public IReadOnlyList<MediaLoadFailure> MediaLoadFailures
    {
        get { lock (_syncRoot) { return _current.Failures; } }
    }

    /// <summary>
    /// Első betöltés: beolvassa a konfigurációt, és felépíti az animációkat. Amíg nem fejeződik be,
    /// a <see cref="GetMovementAnimation"/> <c>null</c>-t, a <see cref="GetOverlayAnimations"/> üres listát ad.
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default) => ReloadAsync(cancellationToken);

    /// <summary>
    /// Újraolvassa a konfigurációt a tárolóból, felépíti az új animációkat a záron kívül, majd a zár alatt
    /// egyben lecseréli a régieket, és kiváltja a <see cref="ConfigurationChanged"/> eseményt.
    /// A méret a mentett <see cref="CharacterSettings"/> szerinti lesz, a nem mentett <see cref="SetCharacterSize"/>
    /// felülíródik. Ha a tároló hibát dob, a futó konfiguráció változatlan marad.
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var token = linked.Token;

        await _rebuildGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var data = (await _store.LoadAsync(token).ConfigureAwait(false)).Data;
            int width = ConfigurationLimits.CharacterSizePx.Clamp(data.Character.CharacterWidth);
            int height = ConfigurationLimits.CharacterSizePx.Clamp(data.Character.CharacterHeight);

            int version;
            lock (_syncRoot)
            {
                _characterWidth = width;
                _characterHeight = height;
                version = ++_sizeVersion;
            }

            var built = await _builder.BuildAsync(data, width, height, token).ConfigureAwait(false);

            lock (_syncRoot)
            {
                _current = built;

                // A felépítés alatt állított méret: az új animációk azonnal ezt mutatják, a képkockákat a
                // közben elindult újraskálázás cseréli.
                if (version != _sizeVersion)
                {
                    ResizeAll(built);
                }
            }
        }
        finally
        {
            _rebuildGate.Release();
        }

        OnConfigurationChanged();
    }

    /// <summary>
    /// Beállítja a karakter és az összes animáció egységes méretét, és kiváltja a <see cref="ConfigurationChanged"/>
    /// eseményt. Az animációk azonnal az új méretben jelennek meg, a képkockákat a háttérben skálázza újra a motor,
    /// és helyben cseréli, így az animáció példányok megmaradnak. A méret a
    /// <see cref="ConfigurationLimits.CharacterSizePx"/> tartományra korlátozódik; a nem pozitív érték nem változtat.
    /// </summary>
    public void SetCharacterSize(int width, int height) => _ = SetCharacterSizeAsync(width, height);

    /// <summary>
    /// Mint a <see cref="SetCharacterSize"/>; a visszaadott feladat az újraskálázás végén fejeződik be. Nem dob kivételt:
    /// ha az újraskálázás meghiúsul, az animációk a régi képkockákat mutatják az új méretben.
    /// </summary>
    public Task SetCharacterSizeAsync(int width, int height)
    {
        int version;
        lock (_syncRoot)
        {
            int newWidth = width > 0 ? ConfigurationLimits.CharacterSizePx.Clamp(width) : _characterWidth;
            int newHeight = height > 0 ? ConfigurationLimits.CharacterSizePx.Clamp(height) : _characterHeight;
            if (newWidth == _characterWidth && newHeight == _characterHeight)
            {
                return Task.CompletedTask;
            }

            _characterWidth = newWidth;
            _characterHeight = newHeight;
            version = ++_sizeVersion;
            ResizeAll(_current);
        }

        OnConfigurationChanged();
        return RescaleAsync(version);
    }

    /// <summary>
    /// Kiértékeli a viselkedési szabályokat a registry értékei alapján (schema.md, "Kiértékelés"), és az aktív
    /// overlay animációkat ZIndex szerint növekvő sorrendben adja vissza.
    /// </summary>
    public IReadOnlyList<IAnimation> GetOverlayAnimations(OsEventDataRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        AnimationSet current;
        lock (_syncRoot)
        {
            current = _current;
        }

        // A pillanatkép nem változik, ezért a kiértékelés a záron kívül futhat.
        var active = RuleEvaluator.SelectActive(current.Rules, r => r.Rule, registry, current.DisabledMetrics);
        return active.ConvertAll(r => (IAnimation)r.Animation);
    }

    /// <summary>
    /// Visszaadja a megadott mozgásállapothoz konfigurált 1 db animációt; az első betöltés előtt <c>null</c>.
    /// </summary>
    public IAnimation? GetMovementAnimation(MovementState state)
    {
        lock (_syncRoot)
        {
            return _current.Movement.GetValueOrDefault(state);
        }
    }

    /// <summary>
    /// Lépteti az összes animációt (a motor időzítője hívja).
    /// </summary>
    public void UpdateAnimations(TimeSpan deltaTime)
    {
        lock (_syncRoot)
        {
            foreach (var binding in _current.Bindings)
            {
                binding.Animation.Update(deltaTime);
            }
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

        _store.Changed -= OnStoreChanged;
        _lifetime.Cancel();
    }

    private async Task RescaleAsync(int version)
    {
        try
        {
            await _rebuildGate.WaitAsync(_lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            AnimationSet current;
            int width;
            int height;
            lock (_syncRoot)
            {
                if (version != _sizeVersion)
                {
                    return; // közben újabb méret jött, annak az újraskálázása elvégzi
                }

                (current, width, height) = (_current, _characterWidth, _characterHeight);
            }

            var apply = await _builder.PrepareRescaleAsync(current.Bindings, width, height, _lifetime.Token).ConfigureAwait(false);

            lock (_syncRoot)
            {
                if (version == _sizeVersion && ReferenceEquals(current, _current))
                {
                    apply();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Trace.TraceError($"A karakterméret-váltás utáni újraskálázás nem sikerült: {ex}");
        }
        finally
        {
            _rebuildGate.Release();
        }
    }

    private void OnStoreChanged(object? sender, EventArgs e) => _ = ReloadInBackgroundAsync();

    private async Task ReloadInBackgroundAsync()
    {
        try
        {
            await ReloadAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            // A futó konfiguráció megmarad; a hiba a következő mentéskor vagy indításkor újra előjön.
            Trace.TraceError($"A konfiguráció újratöltése nem sikerült: {ex}");
        }
    }

    private void ResizeAll(AnimationSet set)
    {
        foreach (var binding in set.Bindings)
        {
            binding.Animation.SetSize(_characterWidth, _characterHeight);
        }
    }

    private void OnConfigurationChanged()
    {
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
}
