namespace ConfigurationEngine.View;

using System.Collections.ObjectModel;
using System.Text.Json;
using ConfigurationEngine.Model;
using ConfigurationEngine.Storage;
using ConfigurationEngine.Validation;

/// <summary>
/// A konfigurációs ablak fő nézetmodellje (docs/configuration_engine/gui.md): betölti a szerkesztett másolatot,
/// mutatja a kihagyott elemeket, összesíti a hibákat, és végrehajtja a Mentést és az Elvetést.
/// A Konfigurációs motort csak az <see cref="IConfigurationEditor"/> homlokzaton (Wrapper Facade) keresztül éri el.
/// A UI szálon fut; a lassú műveleteket a homlokzat a háttérben végzi (Half Sync / Half Async).
/// </summary>
public sealed class ConfigurationWindowViewModel : ObservableObject
{
    /// <summary>A fülek indexe a <c>TabControl</c>-ban, a gui.md "Áttekintés" táblázatának sorrendjében.</summary>
    public const int CharacterTab = 0;
    public const int RulesTab = 1;
    public const int SourcesTab = 2;
    public const int PollingTab = 3;

    private static readonly string[] StateProperties =
    [
        nameof(IsBusy),
        nameof(IsDirty),
        nameof(IsEditable),
        nameof(CanSave),
        nameof(StatusText),
        nameof(Errors),
        nameof(ErrorCount),
        nameof(HasErrors),
        nameof(ErrorCountText),
        nameof(CharacterTabHeader),
        nameof(RulesTabHeader),
        nameof(SourcesTabHeader),
        nameof(PollingTabHeader),
        nameof(HasDroppedItems),
        nameof(DatabaseWasReset),
        nameof(ErrorMessage),
        nameof(HasWarning),
        nameof(WarningText)
    ];

    private readonly IConfigurationEditor _editor;
    private readonly IUserDialogs _dialogs;

    /// <summary>A tallózás után és mentéskor talált médiahibák, a forrás azonosítója szerint.</summary>
    private readonly Dictionary<int, IReadOnlyList<ValidationError>> _mediaErrors = [];

    /// <summary>
    /// A beviteli hibák (gui.md, "Mezőnkénti hibajelzés"): a mező szövege nem alakítható át, ezért nem került
    /// a szerkesztett másolatba. Ugyanúgy hibának számítanak, mint a validátor hibái.
    /// </summary>
    private readonly Dictionary<(ConfigurationItemKind Kind, string ItemKey, string PropertyName), ValidationError> _inputErrors = [];

    private ConfigurationData? _data;
    private string _savedSnapshot = string.Empty;
    private (int Width, int Height) _savedSize;

    /// <summary>A szolgáltatáson azonnal beállított, még nem mentett méret (gui.md, "Karakter mérete").</summary>
    private (int Width, int Height)? _appliedSize;

    private ValidationResult _validation = ValidationResult.Success;
    private IReadOnlyList<ValidationError> _errors = [];
    private bool _isBusy;
    private bool _isDirty;
    private bool _isDetailsOpen;
    private bool _databaseWasReset;
    private int _selectedTabIndex;
    private string? _errorMessage;
    private string? _statusMessage;

    public ConfigurationWindowViewModel(IConfigurationEditor editor, IUserDialogs dialogs)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        SaveCommand = new AsyncRelayCommand(() => SaveAsync(), () => CanSave);
        DiscardCommand = new AsyncRelayCommand(DiscardAsync, () => IsEditable);
        ToggleDetailsCommand = new RelayCommand(() => IsDetailsOpen = !IsDetailsOpen);
        GoToFirstErrorCommand = new RelayCommand(GoToFirstError, () => HasErrors);
    }

    /// <summary>A szerkesztett másolat lecserélése után a fülek ebből építik újra a soraikat.</summary>
    public event EventHandler<DataReloadedEventArgs>? DataReloaded;

    /// <summary>
    /// Az „N hiba” feliratra kattintáskor: a View az első hibás fülre vált, és a fül a hibás mezőre ugrik.
    /// </summary>
    public event EventHandler<ValidationError>? ErrorNavigationRequested;

    /// <summary>A szerkesztett másolat; <c>null</c>, amíg nincs betöltve, vagy ha a betöltés nem sikerült.</summary>
    public ConfigurationData? Data => _data;

    public ObservableCollection<DroppedItemViewModel> DroppedItems { get; } = [];

    public bool HasDroppedItems => DroppedItems.Count > 0;

    /// <summary>Az adatbázis hiányzott vagy sérült volt, ezért alapértékekkel jött létre.</summary>
    public bool DatabaseWasReset => _databaseWasReset;

    /// <summary>Betöltési vagy mentési hiba a figyelmeztető sávban.</summary>
    public string? ErrorMessage => _errorMessage;

    public bool HasWarning => HasDroppedItems || DatabaseWasReset || ErrorMessage is not null;

    /// <summary>
    /// A figyelmeztető sáv szövege (gui.md, "Kihagyott elemek"): „N elem kimaradt betöltéskor”; a mentéskori
    /// törlést a Részletek panel és a mentés előtti megerősítés jelzi.
    /// </summary>
    public string WarningText
    {
        get
        {
            var parts = new List<string>();
            if (ErrorMessage is not null)
            {
                parts.Add(ErrorMessage);
            }

            if (DatabaseWasReset)
            {
                parts.Add("A konfigurációs adatbázis hiányzott vagy sérült volt, ezért alapértékekkel jött létre "
                    + "(a sérült fájl config.db.bak néven megmaradt).");
            }

            if (HasDroppedItems)
            {
                parts.Add($"{DroppedItems.Count} elem kimaradt betöltéskor");
            }

            return string.Join(" ", parts);
        }
    }

    /// <summary>A Részletek panel nyitva van (a Részletek gomb nyitja és zárja).</summary>
    public bool IsDetailsOpen
    {
        get => _isDetailsOpen;
        set => SetProperty(ref _isDetailsOpen, value);
    }

    /// <summary>A kiválasztott fül indexe (<see cref="CharacterTab"/>, …).</summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    /// <summary>Az összes aktuális hiba: a beviteli, a validátor és a médiaellenőrzés hibái.</summary>
    public IReadOnlyList<ValidationError> Errors => _errors;

    public int ErrorCount => _errors.Count;

    public bool HasErrors => ErrorCount > 0;

    /// <summary>A Mentés gomb melletti, kattintható felirat (gui.md, "Mezőnkénti hibajelzés").</summary>
    public string ErrorCountText => $"{ErrorCount} hiba";

    public string CharacterTabHeader => TabHeader("Karakter", CharacterTab);

    public string RulesTabHeader => TabHeader("Szabályok", RulesTab);

    public string SourcesTabHeader => TabHeader("Források", SourcesTab);

    public string PollingTabHeader => TabHeader("Mintavételezés", PollingTab);

    public bool IsBusy => _isBusy;

    /// <summary>A szerkesztett másolat eltér a legutóbb mentett állapottól.</summary>
    public bool IsDirty => _isDirty;

    /// <summary>Van betöltött másolat, és nem fut mentés vagy betöltés.</summary>
    public bool IsEditable => Data is not null && !IsBusy;

    /// <summary>A Mentés (és a Ctrl+S) akkor aktív, ha a másolat eltér a mentett állapottól, és nincs hiba (gui.md, "Mentés").</summary>
    public bool CanSave => IsEditable && IsDirty && !HasErrors;

    /// <summary>
    /// Az alsó sáv bal oldali felirata (gui.md, "Mentés"): „Mentés…” a mentés idejére, „Mentve” utána,
    /// a következő módosításig.
    /// </summary>
    public string StatusText => _statusMessage ?? string.Empty;

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand DiscardCommand { get; }

    public RelayCommand ToggleDetailsCommand { get; }

    public RelayCommand GoToFirstErrorCommand { get; }

    /// <summary>A hiba fülének indexe.</summary>
    public static int TabOf(ConfigurationItemKind kind) => kind switch
    {
        ConfigurationItemKind.CharacterSettings or ConfigurationItemKind.MovementAnimation => CharacterTab,
        ConfigurationItemKind.BehaviorRule => RulesTab,
        ConfigurationItemKind.AnimationSource => SourcesTab,
        ConfigurationItemKind.MetricPolling => PollingTab,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>Az ablak megnyitásakor: betölti a tárolt konfigurációt.</summary>
    public Task LoadAsync() => LoadAsync(DataReloadReason.Opened);

    /// <summary>
    /// A fülek minden módosítás után hívják: újrafuttatja a validátort a teljes szerkesztett másolaton,
    /// és frissíti a hibák számát, a módosítottság jelzését és a Mentés gomb állapotát.
    /// </summary>
    public void NotifyEdited()
    {
        if (Data is null)
        {
            return;
        }

        _statusMessage = null;
        Revalidate();
        _isDirty = Snapshot(Data) != _savedSnapshot;
        RefreshState();
    }

    /// <summary>
    /// Egy mező beviteli hibája (a szöveg nem alakítható át, pl. nem szám vagy hibás <c>HH:mm</c>), vagy
    /// <c>null</c> üzenettel a hiba megszűnése. Amíg van beviteli hiba, a Mentés inaktív.
    /// </summary>
    public void SetInputError(ConfigurationItemKind kind, string itemKey, string propertyName, string? message)
    {
        var key = (kind, itemKey, propertyName);
        if (message is null)
        {
            _inputErrors.Remove(key);
        }
        else
        {
            _inputErrors[key] = new ValidationError(kind, itemKey, propertyName, message);
        }

        UpdateErrors();
        RefreshState();
    }

    /// <summary>Egy elem összes beviteli hibájának törlése (pl. a sor törlésekor).</summary>
    public void ClearInputErrors(ConfigurationItemKind kind, string itemKey)
    {
        foreach (var key in _inputErrors.Keys.Where(k => k.Kind == kind && k.ItemKey == itemKey).ToList())
        {
            _inputErrors.Remove(key);
        }

        UpdateErrors();
        RefreshState();
    }

    /// <summary>
    /// A karakter méretét azonnal érvényesíti a szolgáltatáson (a futó karakteren és az előnézeteken is),
    /// de tárolni csak a Mentés fogja (gui.md, "Karakter mérete").
    /// </summary>
    public void ApplyCharacterSize(int width, int height)
    {
        _editor.SetCharacterSize(width, height);
        _appliedSize = (width, height);
    }

    /// <summary>A tallózás után lefutott médiaellenőrzés eredménye egy forrásra.</summary>
    public void SetMediaErrors(int sourceId, IReadOnlyList<ValidationError> errors)
    {
        if (errors.Count == 0)
        {
            _mediaErrors.Remove(sourceId);
        }
        else
        {
            _mediaErrors[sourceId] = errors;
        }

        NotifyEdited();
    }

    /// <summary>
    /// Mentés (gui.md, "Mentés"): megerősítés kihagyott elemek esetén, zárolás, teljes ellenőrzés, tárolás, majd a
    /// mentett állapot újratöltése. Igaz, ha a mentés sikerült.
    /// </summary>
    public async Task<bool> SaveAsync()
    {
        if (!CanSave || Data is not { } data)
        {
            return false;
        }

        // 1. Megerősítés kihagyott elemek esetén.
        if (HasDroppedItems && !_dialogs.Confirm("Mentés", DroppedItemsConfirmation()))
        {
            return false;
        }

        // 2. Zárolás.
        _isBusy = true;
        _statusMessage = "Mentés…";
        _errorMessage = null;
        RefreshState();
        try
        {
            // 3. Teljes ellenőrzés: a validátor és minden forrás Full médiaellenőrzése.
            Revalidate();
            try
            {
                await ValidateAllMediaAsync(data);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _errorMessage = $"A mentés nem sikerült: {ex.Message} A módosítások megmaradtak, a mentés megismételhető.";
                _statusMessage = null;
                return false;
            }

            if (HasErrors)
            {
                _statusMessage = null;
                return false;
            }

            // 4–5. Tárolás; a motor ezután maga tölt újra (a View ebben nem vesz részt).
            try
            {
                await _editor.SaveAsync(data);
            }
            catch (ConfigurationValidationException ex)
            {
                _validation = ex.Result;
                UpdateErrors();
                _statusMessage = null;
                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _errorMessage = $"A mentés nem sikerült: {ex.Message} A módosítások megmaradtak, a mentés megismételhető.";
                _statusMessage = null;
                return false;
            }

            // 6. A View frissítése a mentett állapottal.
            _appliedSize = null;
            try
            {
                Apply(await _editor.LoadAsync(), DataReloadReason.Saved);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A mentés sikerült: a szerkesztett másolat (a visszaírt azonosítókkal) a mentett állapot,
                // a kihagyott elemek pedig törlődtek (gui.md, "Mentés" 6. lépés).
                MarkSaved(data);
                DroppedItems.Clear();
                _errorMessage = $"A mentés sikerült, de a mentett állapot újratöltése nem: {ex.Message}";
            }

            _statusMessage = "Mentve";
            return true;
        }
        finally
        {
            _isBusy = false;
            RefreshState();
        }
    }

    /// <summary>
    /// Elvetés: megerősítés után visszaállítja a mentett méretet a szolgáltatáson, eldobja az előnézeteket,
    /// és újratölti a mentett állapotot.
    /// </summary>
    public async Task DiscardAsync()
    {
        if (IsDirty && !_dialogs.Confirm("Elvetés", "Elveted a mentetlen módosításokat?"))
        {
            return;
        }

        RestoreSavedSize();
        _editor.ReleasePreviewAnimations();
        await LoadAsync(DataReloadReason.Discarded);
    }

    /// <summary>
    /// Az ablak bezárása előtt: mentetlen módosításnál megkérdezi, hogy menti, elveti, vagy nyitva hagyja az ablakot.
    /// Igaz, ha az ablak bezárható.
    /// </summary>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        if (!IsDirty)
        {
            return true;
        }

        switch (_dialogs.AskUnsavedChanges())
        {
            case CloseDecision.Save:
                return await SaveAsync();
            case CloseDecision.Discard:
                RestoreSavedSize();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Az ablak bezárásakor: eldobja az összes előnézeti animációt.</summary>
    public void OnWindowClosed() => _editor.ReleasePreviewAnimations();

    private async Task LoadAsync(DataReloadReason reason)
    {
        _isBusy = true;
        _statusMessage = null;
        RefreshState();
        try
        {
            Apply(await _editor.LoadAsync(), reason);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _errorMessage = $"A konfiguráció nem tölthető be: {ex.Message}";
        }
        finally
        {
            _isBusy = false;
            RefreshState();
        }
    }

    private void Apply(ConfigurationLoadResult result, DataReloadReason reason)
    {
        _data = result.Data;
        _databaseWasReset = result.DatabaseWasReset;
        _errorMessage = null;
        _isDetailsOpen = false;

        DroppedItems.Clear();
        foreach (var item in result.DroppedItems)
        {
            DroppedItems.Add(new DroppedItemViewModel(item));
        }

        MarkSaved(result.Data);
        OnPropertyChanged(nameof(Data));
        OnPropertyChanged(nameof(IsDetailsOpen));
        DataReloaded?.Invoke(this, new DataReloadedEventArgs(reason));
    }

    /// <summary>A megadott másolatot tekinti a mentett állapotnak.</summary>
    private void MarkSaved(ConfigurationData saved)
    {
        _savedSnapshot = Snapshot(saved);
        _savedSize = (saved.Character.CharacterWidth, saved.Character.CharacterHeight);
        _isDirty = false;
        _mediaErrors.Clear();
        _inputErrors.Clear();
        Revalidate();
    }

    private async Task ValidateAllMediaAsync(ConfigurationData data)
    {
        foreach (var source in data.AnimationSources.ToList())
        {
            var errors = await _editor.ValidateMediaAsync(source);
            if (errors.Count == 0)
            {
                _mediaErrors.Remove(source.Id);
            }
            else
            {
                _mediaErrors[source.Id] = errors;
            }
        }

        UpdateErrors();
    }

    private void GoToFirstError()
    {
        var first = _errors
            .Select((error, index) => (error, index))
            .OrderBy(e => TabOf(e.error.Kind))
            .ThenBy(e => e.index)
            .Select(e => e.error)
            .FirstOrDefault();
        if (first is null)
        {
            return;
        }

        SelectedTabIndex = TabOf(first.Kind);
        ErrorNavigationRequested?.Invoke(this, first);
    }

    private void RestoreSavedSize()
    {
        if (_appliedSize is { } applied && applied != _savedSize)
        {
            _editor.SetCharacterSize(_savedSize.Width, _savedSize.Height);
        }

        _appliedSize = null;
    }

    private void Revalidate()
    {
        _validation = Data is null ? ValidationResult.Success : _editor.Validate(Data);
        UpdateErrors();
    }

    private void UpdateErrors()
    {
        // A törölt források médiahibái már nem érvényesek.
        var sourceIds = Data?.AnimationSources.Select(s => s.Id).ToHashSet() ?? [];
        foreach (var id in _mediaErrors.Keys.Where(id => !sourceIds.Contains(id)).ToList())
        {
            _mediaErrors.Remove(id);
        }

        _errors = [.. _inputErrors.Values, .. _validation.Errors, .. _mediaErrors.Values.SelectMany(e => e)];
    }

    private string DroppedItemsConfirmation() =>
        $"Betöltéskor {DroppedItems.Count} elem kimaradt:\n\n"
        + string.Join("\n", DroppedItems.Select(d => "• " + d.Summary))
        + "\n\nMentéskor ezek véglegesen törlődnek. Folytatod a mentést?";

    private string TabHeader(string title, int tab)
    {
        var count = _errors.Count(e => TabOf(e.Kind) == tab);
        return count == 0 ? title : $"{title} ({count})";
    }

    private void RefreshState()
    {
        foreach (var name in StateProperties)
        {
            OnPropertyChanged(name);
        }

        SaveCommand.RaiseCanExecuteChanged();
        DiscardCommand.RaiseCanExecuteChanged();
        GoToFirstErrorCommand.RaiseCanExecuteChanged();
    }

    private static string Snapshot(ConfigurationData data) => JsonSerializer.Serialize(data);
}
