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
}
