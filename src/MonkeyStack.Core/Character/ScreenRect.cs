namespace MonkeyStack.Core.Character;

/// <summary>
/// Képernyő-téglalap WPF egységben (DIP). Saját típus, hogy a közös szerződés ne függjön a <c>System.Windows.Rect</c>-től.
/// </summary>
/// <param name="Left">Bal szél.</param>
/// <param name="Top">Felső szél.</param>
/// <param name="Right">Jobb szél.</param>
/// <param name="Bottom">Alsó szél.</param>
public readonly record struct ScreenRect(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;
}
