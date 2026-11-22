namespace ConfigurationEngine.Media;

/// <summary>
/// Akkor dobódik, ha egy animációs forrás képkockái nem tölthetők be.
/// Az üzenet a felhasználónak megjeleníthető, magyar nyelvű.
/// </summary>
public sealed class MediaLoadException : Exception
{
    public MediaLoadException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Az <see cref="Model.AnimationSource"/> hibás mezőjének neve (pl. <c>nameof(AnimationSource.Path)</c>),
    /// hogy a konfigurációs ablak a megfelelő mezőnél jelezze a hibát; <c>null</c>, ha a forrás egészére vonatkozik.
    /// </summary>
    public string? PropertyName { get; init; }
}
