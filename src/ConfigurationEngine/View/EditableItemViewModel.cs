namespace ConfigurationEngine.View;

using System.Collections;
using System.ComponentModel;
using ConfigurationEngine.Validation;

/// <summary>
/// A szerkeszthető elemek (karakter beállítások, mozgási animáció, szabály, forrás, mintavételezés) nézetmodelljeinek
/// közös alapja (view.md, "Mezőnkénti hibajelzés"): a hibákat a <c>Kind</c> + <c>ItemKey</c> + <c>PropertyName</c>
/// hármas szerint rendeli a mezőkhöz. A szöveges mezők hibái az <see cref="InputField"/>-eken, a többi mezőé
/// az <see cref="INotifyDataErrorInfo"/>-n és a <see cref="FieldErrors"/>-on, a <c>PropertyName = null</c> hibák
/// az <see cref="ItemErrors"/>-ban (a részletek panel tetején) jelennek meg; a sor hibajele a <see cref="HasAnyError"/>.
/// </summary>
public abstract class EditableItemViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly List<InputField> _fields = [];
    private Dictionary<string, IReadOnlyList<string>> _propertyErrors = [];
    private IReadOnlyList<string> _itemErrors = [];
    private bool _hasAnyError;

    protected EditableItemViewModel(ConfigurationWindowViewModel owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>A View kéri, hogy a megadott mező kapja meg a fókuszt (az „N hiba” feliratra kattintáskor).</summary>
    public event EventHandler? FocusRequested;

    /// <summary>Az elem típusa a <see cref="ValidationError.Kind"/>-hoz.</summary>
    public abstract ConfigurationItemKind Kind { get; }

    /// <summary>Az elem aktuális kulcsa a <see cref="ValidationError.ItemKey"/>-hez.</summary>
    public abstract string ItemKey { get; }

    /// <summary>A nem szöveges mezők hibái, a mező alatti felirathoz.</summary>
    public FieldErrorLookup FieldErrors { get; } = new();

    /// <summary>Az elem egészére vonatkozó hibák (<c>PropertyName = null</c>).</summary>
    public IReadOnlyList<string> ItemErrors => _itemErrors;

    public bool HasItemErrors => _itemErrors.Count > 0;

    /// <summary>Az elemnek van bármilyen hibája: a sor hibajele a listában.</summary>
    public bool HasAnyError => _hasAnyError;

    public bool HasErrors => _propertyErrors.Count > 0;

    /// <summary>A fókuszra váró mező neve; a mezőt megjelenítő vezérlő törli, amikor megkapta a fókuszt.</summary>
    public string? PendingFocus { get; set; }

    protected ConfigurationWindowViewModel Owner { get; }

    /// <summary>A szöveges mezők, a megjelenítés sorrendjében.</summary>
    protected IReadOnlyList<InputField> Fields => _fields;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && _propertyErrors.TryGetValue(propertyName, out var errors) ? errors : Array.Empty<string>();

    /// <summary>A megadott mezőre (vagy ha nincs megadva, az első mezőre) viszi a fókuszt.</summary>
    public void RequestFocus(string? propertyName)
    {
        PendingFocus = propertyName ?? _fields.FirstOrDefault()?.PropertyName ?? string.Empty;
        FocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Kiválogatja az elem hibáit az összes hiba közül a <see cref="ValidationResult.ErrorsFor(ConfigurationItemKind, string)"/>
    /// segítségével (view.md, "Mezőnkénti hibajelzés"), és a mezőkhöz rendeli őket.
    /// </summary>
    public void ApplyErrors(ValidationResult errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var (kind, key) = (Kind, ItemKey);
        var mine = errors.ErrorsFor(kind, key).ToList();

        var fieldNames = new HashSet<string>();
        foreach (var field in _fields)
        {
            fieldNames.Add(field.PropertyName);
            field.SetErrors(errors.ErrorsFor(kind, key, field.PropertyName).Select(e => e.Message).ToList());
        }

        var propertyErrors = mine
            .Where(e => e.PropertyName is not null && !fieldNames.Contains(e.PropertyName))
            .GroupBy(e => e.PropertyName!)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(e => e.Message).ToList());
        var changed = _propertyErrors.Keys.Union(propertyErrors.Keys)
            .Where(name => !_propertyErrors.TryGetValue(name, out var before)
                || !propertyErrors.TryGetValue(name, out var after)
                || !before.SequenceEqual(after))
            .ToList();
        _propertyErrors = propertyErrors;
        foreach (var name in changed)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(name));
        }

        if (changed.Count > 0)
        {
            FieldErrors.Set(propertyErrors);
            OnPropertyChanged(nameof(HasErrors));
        }

        var itemErrors = mine.Where(e => e.PropertyName is null).Select(e => e.Message).ToList();
        if (!itemErrors.SequenceEqual(_itemErrors))
        {
            _itemErrors = itemErrors;
            OnPropertyChanged(nameof(ItemErrors));
            OnPropertyChanged(nameof(HasItemErrors));
        }

        if (_hasAnyError != mine.Count > 0)
        {
            _hasAnyError = mine.Count > 0;
            OnPropertyChanged(nameof(HasAnyError));
        }
    }

    /// <summary>A szöveges mezők újraolvasása a modellből (a beviteli hibás mezők kivételével).</summary>
    public void RefreshFields()
    {
        foreach (var field in _fields)
        {
            field.Refresh();
        }
    }

    /// <summary>
    /// A sor törlésekor: a beviteli hibái is megszűnnek (a hozzá tartozó előnézetet a leszármazott dobja el).
    /// </summary>
    public virtual void OnRemoved() => Owner.ClearInputErrors(Kind, ItemKey);

    /// <summary>Egy szöveges mező sikeres beírása után, a <see cref="ConfigurationWindowViewModel.NotifyEdited"/> előtt.</summary>
    protected virtual void OnFieldCommitted(string propertyName)
    {
    }

    /// <summary>Egy nem szöveges mező (legördülő lista, jelölőnégyzet) módosítása után.</summary>
    protected void Edited() => Owner.NotifyEdited();

    protected InputField Field(string propertyName, Func<string> read, Func<string, string?> write)
    {
        var field = new InputField(propertyName, read, write, inputError =>
        {
            Owner.SetInputError(Kind, ItemKey, propertyName, inputError);
            if (inputError is null)
            {
                OnFieldCommitted(propertyName);
                Owner.NotifyEdited();
            }
        });
        _fields.Add(field);
        return field;
    }

    protected InputField IntField(string propertyName, Func<int> get, Action<int> set) =>
        Field(propertyName, () => InputField.Format(get()), InputField.IntWriter(set));

    protected InputField OptionalIntField(string propertyName, Func<int?> get, Action<int?> set) =>
        Field(propertyName, () => InputField.Format(get()), InputField.OptionalIntWriter(set));

    protected InputField DoubleField(string propertyName, Func<double> get, Action<double> set) =>
        Field(propertyName, () => InputField.Format(get()), InputField.DoubleWriter(set));
}
