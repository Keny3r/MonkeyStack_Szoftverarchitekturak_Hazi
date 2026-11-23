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
using MonkeyStack.Core.Timing;

/// <summary>
/// A Konfigurációs motor Modell rétege (Component Configurator / Monitor Object minta).
/// Feladata:
/// 1. A konfiguráció betöltése a tárolóból, és újratöltése minden sikeres mentés után (<see cref="IConfigurationStore.Changed"/>).
/// 2. A képek fix méretűre (CharacterWidth x CharacterHeight) skálázása és Animation objektumok létrehozása.
/// 3. Az összes Animation objektum (és a konfigurációs ablak előnézeteinek) folyamatos pörgetése a közös
///    <see cref="IFrameLoop"/> ütemére (UpdateAnimations).
/// 4. Állapotmentes kiértékelés: a Karakter által lekérdezett mozgás- (1 db IAnimation)
///    és overlay animációk (ZIndex szerint rendezett IReadOnlyList&lt;IAnimation&gt;) visszaadása.
/// Három interfészt valósít meg (Extension Interface minta): a Karakter az <see cref="IConfigurationService"/>-t, az
/// OS motor az <see cref="IPollingSettingsProvider"/>-t, a konfigurációs ablak az <see cref="IConfigurationEditor"/>-t látja.
/// Leírás: docs/configuration_engine/service.md és docs/configuration_engine/editor.md.
/// </summary>
public sealed class ConfigurationService : IConfigurationService, IPollingSettingsProvider, IConfigurationEditor, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly IConfigurationStore _store;
    private readonly AnimationSetBuilder _builder;
    private readonly IMediaValidator _mediaValidator;
    private readonly IConfigurationValidator _validator = new ConfigurationValidator();
    private readonly IFrameLoop _frameLoop;
    private readonly PreviewAnimations _previews;

    /// <summary>
    /// Sorosítja az újratöltést és az újraskálázást; a lassú képbetöltés e mögött, a <see cref="_syncRoot"/>-on kívül
    /// fut. A <see cref="Dispose"/> szándékosan nem dobja el: a még futó háttérmunka a végén elengedi, és várakozó
    /// leíró nélkül a <see cref="SemaphoreSlim"/> nem foglal eldobandó erőforrást.
    /// </summary>
    private readonly SemaphoreSlim _rebuildGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();

    private AnimationSet _current = AnimationSet.Empty;

    /// <summary>A legutóbbi méretváltáskor az új méretre be nem tölthető források (az animációjuk a régi képkockákat mutatja).</summary>
    private IReadOnlyList<MediaLoadFailure> _rescaleFailures = [];
    private int _characterWidth = ConfigurationDefaults.CreateCharacterSettings().CharacterWidth;
    private int _characterHeight = ConfigurationDefaults.CreateCharacterSettings().CharacterHeight;

    /// <summary>Minden méretváltáskor nő; a korábbi méretre készült, közben elavult felépítés így eldobható.</summary>
    private int _sizeVersion;

    /// <summary>A folyamatban lévő újraskálázás megszakítása; újabb méretváltáskor a régi méret skálázása leáll.</summary>
    private CancellationTokenSource? _rescaleCancellation;
    private bool _disposed;

    /// <summary>
    /// 1, ha egy mentés utáni újratöltés már sorban áll, és még nem kezdte olvasni a tárolót; a további mentések
    /// ilyenkor nem indítanak újat, mert a sorban álló már a legfrissebb adatot olvassa majd.
    /// </summary>
    private int _storeReloadQueued;

    /// <summary>
    /// A felhasználó adatbázisával (<see cref="SqliteConfigurationStore.DefaultDatabasePath"/>) és a szállított képekkel.
    /// Nem végez I/O-t: a konfiguráció az <see cref="InitializeAsync"/> hívásakor töltődik be.
    /// </summary>
    /// <param name="frameLoop">
    /// A közös léptető, amelynek ütemére az animációk lépnek. A host adja (integrált futásnál ugyanaz, amely a Karakter
    /// fizikáját is hajtja); a szolgáltatásnak nincs saját időzítője, és a léptetőt nem indítja el és nem dobja el.
    /// </param>
    public ConfigurationService(IFrameLoop frameLoop)
        : this(CreateDefaultStore(out var mediaValidator), new FrameLoader(), mediaValidator, frameLoop)
    {
    }

    /// <param name="store">A konfiguráció tárolója.</param>
    /// <param name="frameLoader">A képkockák betöltője (az animációkhoz és az előnézetekhez).</param>
    /// <param name="mediaValidator">A konfigurációs ablak teljes médiaellenőrzéséhez és a képméret lekérdezéséhez.</param>
    /// <param name="frameLoop">A közös léptető (lásd a másik konstruktort).</param>
    public ConfigurationService(
        IConfigurationStore store,
        IFrameLoader frameLoader,
        IMediaValidator mediaValidator,
        IFrameLoop frameLoop)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(frameLoader);
        ArgumentNullException.ThrowIfNull(mediaValidator);
        ArgumentNullException.ThrowIfNull(frameLoop);

        _store = store;
        _builder = new AnimationSetBuilder(frameLoader);
        _mediaValidator = mediaValidator;
        _previews = new PreviewAnimations(frameLoader, _characterWidth, _characterHeight);
        _frameLoop = frameLoop;
        _store.Changed += OnStoreChanged;
        _frameLoop.Tick += OnFrameTick;
    }

    /// <summary>
    /// Tüzel, ha a futó konfiguráció megváltozott (újratöltés, méretváltás); a Karakter ekkor újra lekérdezi az
    /// animációkat. Bármelyik szálon kiváltódhat, és sosem a motor zárja alatt.
    /// </summary>
    public event EventHandler? ConfigurationChanged;

    /// <summary>
    /// Tüzel, ha egy újratöltés után legalább egy metrika gyakorisága vagy be-/kikapcsolt állapota megváltozott
    /// (az első betöltés előtt érvényes alapértékekhez képest is). Bármelyik szálon kiváltódhat, sosem a motor zárja alatt.
    /// </summary>
    public event EventHandler? PollingSettingsChanged;

    public int CharacterWidth
    {
        get { lock (_syncRoot) { return _characterWidth; } }
        set => SetCharacterSize(value, 0); // a 0 a másik méretet változatlanul hagyja, így nincs olvasás-írás verseny
    }

    public int CharacterHeight
    {
        get { lock (_syncRoot) { return _characterHeight; } }
        set => SetCharacterSize(0, value);
    }

    /// <summary>
    /// A be nem töltött animációs források. A legutóbbi felépítéskor be nem töltött forrásra hivatkozó mozgásállapot
    /// a szállított alapanimációt kapta, a rá hivatkozó szabályok nem jelennek meg. A legutóbbi méretváltáskor az új
    /// méretre be nem tölthető forrás animációja a régi képkockákat mutatja, az új méretre nyújtva.
    /// </summary>
    public IReadOnlyList<MediaLoadFailure> MediaLoadFailures
    {
        get
        {
            lock (_syncRoot)
            {
                return _rescaleFailures.Count == 0 ? _current.Failures : [.. _current.Failures, .. _rescaleFailures];
            }
        }
    }

    /// <summary>
    /// Első betöltés: beolvassa a konfigurációt, és felépíti az animációkat. Amíg nem fejeződik be,
    /// a <see cref="GetMovementAnimation"/> <c>null</c>-t, a <see cref="GetOverlayAnimations"/> üres listát ad.
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default) => ReloadAsync(cancellationToken);

    /// <summary>
    /// Újraolvassa a konfigurációt a tárolóból, felépíti az új animációkat a záron kívül, majd a zár alatt
    /// egyben lecseréli a régieket, és kiváltja a <see cref="ConfigurationChanged"/> eseményt. Háttérszálon fut,
    /// így a UI szálról blokkolva is várható. A méret a mentett <see cref="CharacterSettings"/> szerinti lesz, a nem
    /// mentett <see cref="SetCharacterSize"/> felülíródik; ha viszont a felépítés közben állítják, az új méret marad.
    /// Ha a tároló vagy a felépítés hibát dob, a futó konfiguráció és a méret változatlan marad.
    /// </summary>
    public Task ReloadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => ReloadCoreAsync(fromStoreChange: false, cancellationToken), CancellationToken.None);

    /// <summary>
    /// Beállítja a karakter és az összes animáció egységes méretét, és egyszer kiváltja a
    /// <see cref="ConfigurationChanged"/> eseményt. Az animációk azonnal az új méretben jelennek meg (a régi képkockák
    /// nyújtva). A háttérben a motor az új méretre tölti be a képkockákat, és ugyanazokba a példányokba cseréli
    /// (<see cref="IMutableAnimation.SetFrames"/>), így a lekérdezett animációk érvényesek maradnak, és a lejátszás
    /// folytatódik. A méret a <see cref="ConfigurationLimits.CharacterSizePx"/> tartományra korlátozódik; a nem pozitív
    /// érték nem változtat.
    /// </summary>
    public void SetCharacterSize(int width, int height) => _ = SetCharacterSizeAsync(width, height);

    /// <summary>
    /// Mint a <see cref="SetCharacterSize"/>; a visszaadott feladat az újraskálázás végén (vagy elavulásakor) fejeződik
    /// be. Nem dob kivételt: ha az újraskálázás meghiúsul, az animációk a régi képkockákat mutatják az új méretben.
    /// </summary>
    public Task SetCharacterSizeAsync(int width, int height)
    {
        int version;
        CancellationToken token;
        CancellationTokenSource? previous;
        lock (_syncRoot)
        {
            int newWidth = width > 0 ? ConfigurationLimits.CharacterSizePx.Clamp(width) : _characterWidth;
            int newHeight = height > 0 ? ConfigurationLimits.CharacterSizePx.Clamp(height) : _characterHeight;
            if (_disposed || (newWidth == _characterWidth && newHeight == _characterHeight))
            {
                return Task.CompletedTask;
            }

            _characterWidth = newWidth;
            _characterHeight = newHeight;
            version = ++_sizeVersion;
            ResizeAll(_current);
            _previews.SetSize(newWidth, newHeight);

            previous = _rescaleCancellation;
            _rescaleCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            token = _rescaleCancellation.Token;
        }

        // A megszakítás a zár után: a Cancel a várakozók folytatásait ezen a szálon is futtathatja, és azok
        // nem futhatnak a motor zárja alatt. Megszakítás után a forrás eldobható.
        previous?.Cancel();
        previous?.Dispose();

        // Előbb indul az újraskálázás, így egy hibás feliratkozó sem akadályozhatja meg.
        var rescale = Task.Run(() => RescaleAsync(version, token), CancellationToken.None);
        OnConfigurationChanged();
        return rescale;
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
    /// Lépteti az összes animációt és a konfigurációs ablak előnézeteit. A közös <see cref="IFrameLoop"/> minden
    /// ütemében magától fut; kézzel csak tesztben érdemes hívni.
    /// </summary>
    public void UpdateAnimations(TimeSpan deltaTime)
    {
        lock (_syncRoot)
        {
            foreach (var animation in _current.Animations)
            {
                animation.Update(deltaTime);
            }
        }

        _previews.Update(deltaTime);
    }

    /// <summary>
    /// A metrika lekérdezési gyakorisága a futó konfiguráció szerint; <c>null</c>, ha a metrika ki van kapcsolva.
    /// Az első betöltés előtt (és a konfigurációból hiányzó metrikánál) a séma alapértéke.
    /// </summary>
    public TimeSpan? GetPollingInterval(Metric metric)
    {
        lock (_syncRoot)
        {
            return PollingInterval(_current.Data, metric);
        }
    }

    /// <summary>A tárolt konfiguráció: minden hívás új, független másolatot ad (a tároló minden betöltéskor újraolvas).</summary>
    public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
        _store.LoadAsync(cancellationToken);

    /// <summary>A mentéssel azonos normalizálás után futtatja a validátort; a <paramref name="data"/> nem változik.</summary>
    public ValidationResult Validate(ConfigurationData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var normalized = data.Copy();
        ModelCopies.Normalize(normalized);
        return _validator.Validate(normalized);
    }

    public Task<IReadOnlyList<ValidationError>> ValidateMediaAsync(
        AnimationSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _mediaValidator.ValidateAsync(source.Copy(), MediaValidationDepth.Full, cancellationToken);
    }

    /// <summary>
    /// Teljes ellenőrzés (a validátor és minden forrás <see cref="MediaValidationDepth.Full"/> médiaellenőrzése), majd
    /// mentés a tárolóba. A tároló a mentés után kiváltja a <see cref="IConfigurationStore.Changed"/> eseményt, amelyre a
    /// szolgáltatás a háttérben újratölt; a visszatérés ezt nem várja meg.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">Ha a konfiguráció érvénytelen; ilyenkor semmi nem módosul.</exception>
    public async Task SaveAsync(ConfigurationData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var normalized = data.Copy();
        ModelCopies.Normalize(normalized);
        var errors = _validator.Validate(normalized).Errors.ToList();
        foreach (var source in normalized.AnimationSources)
        {
            errors.AddRange(await _mediaValidator
                .ValidateAsync(source, MediaValidationDepth.Full, cancellationToken)
                .ConfigureAwait(false));
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(new ValidationResult(errors));
        }

        // A tároló a saját másolatán újra normalizál és validál, és az új azonosítókat a data objektumaiba írja vissza.
        await _store.SaveAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public Task<PixelSize> ReadImageSizeAsync(AnimationSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _mediaValidator.ReadImageSizeAsync(source.Copy(), cancellationToken);
    }

    /// <summary>
    /// Előnézeti animáció a forrás pillanatképéből, az aktuális karakterméretben. A futó konfigurációtól külön
    /// példány: az újratöltés nem érinti, csak a <see cref="ReleasePreviewAnimation"/> és a
    /// <see cref="ReleasePreviewAnimations"/>.
    /// </summary>
    /// <exception cref="MediaLoadException">Ha a forrás nem tölthető be.</exception>
    public Task<IAnimation> CreatePreviewAnimationAsync(
        AnimationSource source,
        double framesPerSecond,
        CancellationToken cancellationToken = default) =>
        _previews.CreateAsync(source, framesPerSecond, cancellationToken);

    public void ReleasePreviewAnimation(IAnimation preview) => _previews.Release(preview);

    public void ReleasePreviewAnimations() => _previews.ReleaseAll();

    /// <summary>A kiadott, még el nem dobott előnézetek száma (tesztekhez).</summary>
    internal int PreviewCount => _previews.Count;

    /// <summary>
    /// Leiratkozik a tárolóról és a léptetőről, eldobja az előnézeteket, és megszakítja a folyamatban lévő betöltést és
    /// újraskálázást. A léptetőt nem dobja el, mert azt a host adta.
    /// </summary>
    public void Dispose()
    {
        CancellationTokenSource? rescale;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            rescale = _rescaleCancellation;
            _rescaleCancellation = null;
        }

        _store.Changed -= OnStoreChanged;
        _frameLoop.Tick -= OnFrameTick;
        _previews.Dispose();

        // Előbb megszakítás, utána eldobás: a még futó háttérmunka így megszakított tokent lát, nem eldobott forrást.
        _lifetime.Cancel();
        rescale?.Dispose();
        _lifetime.Dispose();
    }

    private async Task ReloadCoreAsync(bool fromStoreChange, CancellationToken cancellationToken)
    {
        CancellationToken lifetime;
        lock (_syncRoot)
        {
            // A Dispose a _disposed beállítása után, a záron kívül dobja el a _lifetime-ot, így itt még érvényes.
            ObjectDisposedException.ThrowIf(_disposed, this);
            lifetime = _lifetime.Token;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime);
        var token = linked.Token;
        bool pollingChanged = false;

        await _rebuildGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (fromStoreChange)
            {
                // Innentől egy újabb mentés már új újratöltést igényel, mert ez a mentés előtti adatot is olvashatja.
                Volatile.Write(ref _storeReloadQueued, 0);
            }

            int version;
            lock (_syncRoot)
            {
                version = _sizeVersion;
            }

            var data = (await _store.LoadAsync(token).ConfigureAwait(false)).Data;
            int width = ConfigurationLimits.CharacterSizePx.Clamp(data.Character.CharacterWidth);
            int height = ConfigurationLimits.CharacterSizePx.Clamp(data.Character.CharacterHeight);
            var built = await _builder.BuildAsync(data, width, height, token).ConfigureAwait(false);

            lock (_syncRoot)
            {
                // A Dispose után befejeződő felépítés eredménye már nem kerül be. A _disposed ugyanezen zár alatt
                // íródik, így a Dispose visszatérése után biztosan nincs csere.
                if (_disposed)
                {
                    throw new OperationCanceledException(token);
                }

                token.ThrowIfCancellationRequested();
                pollingChanged = Enum.GetValues<Metric>()
                    .Any(m => PollingInterval(_current.Data, m) != PollingInterval(built.Data, m));
                _current = built;
                _rescaleFailures = [];
                if (version == _sizeVersion)
                {
                    // A mentett méret csak a sikeres csere után érvényes, így a jelentett méret mindig az animációké.
                    (_characterWidth, _characterHeight) = (width, height);
                    _sizeVersion++;
                    _previews.SetSize(width, height);
                }
                else
                {
                    // Felépítés közben állított méret: az új animációk azonnal ezt mutatják, a képkockáikat a
                    // közben elindult újraskálázás tölti be az új méretre.
                    ResizeAll(built);
                }
            }
        }
        finally
        {
            _rebuildGate.Release();
        }

        OnConfigurationChanged();
        if (pollingChanged)
        {
            Raise(PollingSettingsChanged, nameof(PollingSettingsChanged));
        }
    }

    private async Task RescaleAsync(int version, CancellationToken token)
    {
        try
        {
            await _rebuildGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            AnimationSet set;
            int width;
            int height;
            lock (_syncRoot)
            {
                if (version != _sizeVersion)
                {
                    return; // közben újabb méret vagy újratöltés jött, az elvégzi
                }

                (set, width, height) = (_current, _characterWidth, _characterHeight);
            }

            var rescaled = await _builder.RescaleAsync(set, width, height, token).ConfigureAwait(false);

            lock (_syncRoot)
            {
                // A kapu miatt közben nem volt újratöltés, így a set még a futó pillanatkép. Nincs csere és nincs
                // újabb esemény: a lekérdezett példányok kapják meg az új képkockákat.
                if (version == _sizeVersion && !_disposed && !token.IsCancellationRequested)
                {
                    foreach (var (animation, frames) in rescaled.Frames)
                    {
                        animation.SetFrames(frames, width, height);
                    }

                    _rescaleFailures = rescaled.Failures;
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

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _storeReloadQueued, 1) == 0)
        {
            _ = ReloadInBackgroundAsync();
        }
    }

    private async Task ReloadInBackgroundAsync()
    {
        try
        {
            await Task.Run(() => ReloadCoreAsync(fromStoreChange: true, CancellationToken.None)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // A szolgáltatást közben eldobták.
        }
        catch (Exception ex)
        {
            // A futó konfiguráció megmarad; a hiba a következő mentéskor vagy indításkor újra előjön.
            Trace.TraceError($"A konfiguráció újratöltése nem sikerült: {ex}");
        }
    }

    private void ResizeAll(AnimationSet set)
    {
        foreach (var animation in set.Animations)
        {
            animation.SetSize(_characterWidth, _characterHeight);
        }
    }

    /// <summary>
    /// Kiváltja a <see cref="ConfigurationChanged"/> eseményt. A feliratkozók egymástól függetlenül futnak: egy
    /// feliratkozó kivétele nem éri el a többit, és nem teszi sikertelenné a már megtörtént cserét.
    /// </summary>
    private void OnConfigurationChanged() => Raise(ConfigurationChanged, nameof(ConfigurationChanged));

    /// <summary>Kivált egy eseményt úgy, hogy a feliratkozók egymástól függetlenül fussanak (lásd <see cref="OnConfigurationChanged"/>).</summary>
    private void Raise(EventHandler? handlers, string eventName)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Egy {eventName} feliratkozó hibát dobott: {ex}");
            }
        }
    }

    private void OnFrameTick(object? sender, FrameTickEventArgs e) => UpdateAnimations(e.DeltaTime);

    /// <summary>
    /// A metrika gyakorisága a konfiguráció szerint (schema.md, "MetricPolling"); <c>null</c> a kikapcsolt metrikánál.
    /// Konfiguráció nélkül, illetve a hiányzó metrikánál a séma alapértéke.
    /// </summary>
    private static TimeSpan? PollingInterval(ConfigurationData? data, Metric metric)
    {
        var polling = data?.MetricPollings.FirstOrDefault(p => p.Metric == metric);
        if (polling is null)
        {
            return TimeSpan.FromMilliseconds(ConfigurationDefaults.PollingIntervalMs(metric));
        }

        return polling.IsEnabled ? TimeSpan.FromMilliseconds(polling.IntervalMs) : null;
    }

    private static SqliteConfigurationStore CreateDefaultStore(out IMediaValidator mediaValidator)
    {
        mediaValidator = new MediaValidator();
        return new SqliteConfigurationStore(mediaValidator: mediaValidator);
    }
}
