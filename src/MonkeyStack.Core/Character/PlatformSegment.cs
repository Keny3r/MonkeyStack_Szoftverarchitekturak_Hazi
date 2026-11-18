namespace MonkeyStack.Core.Character;

/// <summary>
/// Egy látható ablakperem (járható vízszintes platform-szakasz) reprezentációja a fizikai motor számára.
/// Egy külső komponens (Wrapper Facade) számolja ki az ablakok tetejéből és a takarásokból (occlusion).
/// </summary>
/// <param name="Y">A platform függőleges képernyő-koordinátája.</param>
/// <param name="LeftX">A látható platform-szakasz bal széle.</param>
/// <param name="RightX">A látható platform-szakasz jobb széle.</param>
public readonly record struct PlatformSegment(double Y, double LeftX, double RightX)
{
    public double Width => Math.Max(0, RightX - LeftX);

    /// <summary>
    /// Megvizsgálja, hogy az adott X koordináta a platformon belül van-e a megadott szélső ráhagyással.
    /// </summary>
    public bool ContainsX(double x, double edgeMargin = 0)
    {
        return x >= LeftX + edgeMargin && x <= RightX - edgeMargin;
    }
}
