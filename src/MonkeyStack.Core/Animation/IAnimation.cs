namespace MonkeyStack.Core.Animation;

/// <summary>
/// Generikus animációs interfész a karakter és az overlay megjelenítéséhez.
/// A konkrét implementációkat a grafikai és animációs réteg felelősei valósítják meg.
/// </summary>
public interface IAnimation
{
    void Play();
    void Stop();
}
