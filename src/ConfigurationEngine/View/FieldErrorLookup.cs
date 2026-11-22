namespace ConfigurationEngine.View;

using System.Windows.Data;

/// <summary>
/// A nem szöveges mezők (legördülő listák, jelölőnégyzetek) hibái mezőnév szerint, a mező alatti felirathoz:
/// <c>{Binding FieldErrors[AnimationSourceId]}</c>. Hiányzó mezőnél üres szöveget ad.
/// </summary>
public sealed class FieldErrorLookup : ObservableObject
{
    private IReadOnlyDictionary<string, IReadOnlyList<string>> _errors = new Dictionary<string, IReadOnlyList<string>>();

    public string this[string propertyName] =>
        _errors.TryGetValue(propertyName, out var errors) ? string.Join(Environment.NewLine, errors) : string.Empty;

    internal void Set(IReadOnlyDictionary<string, IReadOnlyList<string>> errors)
    {
        _errors = errors;
        OnPropertyChanged(Binding.IndexerName);
    }
}
