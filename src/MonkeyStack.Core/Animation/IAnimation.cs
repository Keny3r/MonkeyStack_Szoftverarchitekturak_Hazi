namespace MonkeyStack.Core.Animation;

using System.Windows.Media.Imaging;

/// <summary>
/// A közös Modell réteg pozíció nélküli animációs interfésze.
/// Az animációs objektumokat a Konfigurációs motor hozza létre, skálázza, állítja be és pörgeti (Update).
/// Pozíciót nem tárol, így ugyanannak a modellnek a képkockáját a Config View és a Karakter View
/// saját AnimationView példányai egymástól függetlenül tudják kirajzolni.
/// </summary>
public interface IAnimation
{
    /// <summary>
    /// Visszaadja a skálázott animáció szélességét pixelben.
    /// </summary>
    int GetWidth();

    /// <summary>
    /// Visszaadja a skálázott animáció magasságát pixelben.
    /// </summary>
    int GetHeight();

    /// <summary>
    /// Az éppen aktuális képkocka, amelyet a View kirajzolhat.
    /// </summary>
    BitmapSource? CurrentFrame { get; }

    /// <summary>
    /// Az aktuális képkocka indexe.
    /// </summary>
    int CurrentFrameIndex { get; }

    /// <summary>
    /// A képkockák teljes száma az animációban.
    /// </summary>
    int FrameCount { get; }

    /// <summary>
    /// Az animáció lejátszási sebessége (képkocka / másodperc).
    /// </summary>
    double FramesPerSecond { get; set; }

    /// <summary>
    /// Az animáció rétegsorrendje (Z-pozíció) az overlayek rendezéséhez.
    /// </summary>
    int ZIndex { get; set; }

    /// <summary>
    /// Frissíti az animáció megjelenítési méretét (pl. amikor a Config menüben átállítják a karakter méretét).
    /// </summary>
    void SetSize(int width, int height);

    /// <summary>
    /// Lépteti a folyamatosan loopoló animációt az eltelt idő függvényében.
    /// </summary>
    void Update(TimeSpan deltaTime);
}
