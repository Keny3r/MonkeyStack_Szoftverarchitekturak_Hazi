namespace MonkeyStack.Core.Animation;

using System.Windows.Media.Imaging;

/// <summary>
/// A közös Modell réteg pozíció nélküli, csak olvasható animációs interfésze: ezt kapják a nézetek
/// (Karakter View, konfigurációs ablak), amelyek csak kirajzolják az aktuális képkockát.
/// Az animációs objektumokat a Konfigurációs motor hozza létre, skálázza, állítja be és pörgeti; ehhez az
/// <see cref="IMutableAnimation"/> bővítő interfészt használja (Extension Interface minta).
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
    double FramesPerSecond { get; }

    /// <summary>
    /// Az overlay-ek egymás közti rétegsorrendje (Z-pozíció): a kisebbet rajzoljuk előbb. Az alap mozgásanimációnál
    /// nem számít, mert az mindig legalul van, minden overlay alatt.
    /// </summary>
    int ZIndex { get; }
}
