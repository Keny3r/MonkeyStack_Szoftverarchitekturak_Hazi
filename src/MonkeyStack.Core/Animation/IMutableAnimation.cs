namespace MonkeyStack.Core.Animation;

using System.Windows.Media.Imaging;

/// <summary>
/// Az <see cref="IAnimation"/> írható bővítése (Extension Interface minta): beállítás, átméretezés és léptetés.
/// Csak a Konfigurációs motor használja, amely az animációkat létrehozza és pörgeti. A nézetek és a Karakter
/// csak <see cref="IAnimation"/>-ként kapják meg őket, így nem módosíthatják és nem léptethetik őket.
/// </summary>
public interface IMutableAnimation : IAnimation
{
    /// <summary>
    /// Az animáció lejátszási sebessége (képkocka / másodperc).
    /// </summary>
    new double FramesPerSecond { get; set; }

    /// <summary>
    /// Az overlay-ek egymás közti rétegsorrendje (Z-pozíció).
    /// </summary>
    new int ZIndex { get; set; }

    /// <summary>
    /// Frissíti az animáció megjelenítési méretét (pl. amikor a Config menüben átállítják a karakter méretét).
    /// </summary>
    void SetSize(int width, int height);

    /// <summary>
    /// Lecseréli az animáció képkockáit és a skálázott méretét (pl. újraskálázott képkockák esetén).
    /// </summary>
    void SetFrames(IReadOnlyList<BitmapSource> frames, int width, int height);

    /// <summary>
    /// Lépteti a folyamatosan loopoló animációt az eltelt idő függvényében.
    /// </summary>
    void Update(TimeSpan deltaTime);
}
