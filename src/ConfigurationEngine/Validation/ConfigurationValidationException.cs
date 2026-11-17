namespace ConfigurationEngine.Validation;

/// <summary>
/// Akkor dobódik, ha érvénytelen konfigurációt próbálnak menteni.
/// A konfigurációs ablak mentés előtt validál, így ez csak védelmi háló.
/// </summary>
public sealed class ConfigurationValidationException : Exception
{
    public ConfigurationValidationException(ValidationResult result)
        : base($"A konfiguráció érvénytelen ({result.Errors.Count} hiba).")
    {
        Result = result;
    }

    public ValidationResult Result { get; }
}
