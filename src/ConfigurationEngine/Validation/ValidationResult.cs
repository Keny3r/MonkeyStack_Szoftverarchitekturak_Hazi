namespace ConfigurationEngine.Validation;

/// <summary>
/// Egy validálás eredménye.
/// </summary>
public sealed class ValidationResult
{
    public static ValidationResult Success { get; } = new([]);

    public ValidationResult(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    /// <summary>Egy elem hibái, pl. a konfigurációs ablak sorainak kiemeléséhez.</summary>
    public IEnumerable<ValidationError> ErrorsFor(ConfigurationItemKind kind, string itemKey) =>
        Errors.Where(e => e.Kind == kind && e.ItemKey == itemKey);

    /// <summary>Egy mező hibái, a mezőnkénti hibajelzéshez.</summary>
    public IEnumerable<ValidationError> ErrorsFor(ConfigurationItemKind kind, string itemKey, string propertyName) =>
        ErrorsFor(kind, itemKey).Where(e => e.PropertyName == propertyName);
}
