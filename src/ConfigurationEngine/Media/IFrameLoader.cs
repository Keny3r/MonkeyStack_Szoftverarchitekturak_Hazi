namespace ConfigurationEngine.Media;

using System.Windows.Media.Imaging;
using ConfigurationEngine.Model;

/// <summary>
/// Egy animációs forrás képkockáinak betöltése és egységes méretre skálázása.
/// Ebből készülnek a <see cref="MonkeyStack.Core.Animation.Animation"/> példányok
/// (docs/mvc_architecture.md, "Config Engine" – képek fix méretűre skálázása).
/// </summary>
public interface IFrameLoader
{
    /// <summary>
    /// Betölti a forrás képkockáit (GIF, sprite sheet vagy képsorozat-mappa), és
    /// <paramref name="width"/> × <paramref name="height"/> méretre skálázza őket: aránytartóan,
    /// alsó-középre igazítva, a maradék területet átlátszóan kitöltve.
    /// A visszaadott képkockák fagyasztottak (<c>Freeze</c>), így szálak között megoszthatók.
    /// </summary>
    /// <exception cref="MediaLoadException">Ha a forrás nem tölthető be (nem létezik, nem dekódolható, túl nagy).</exception>
    Task<IReadOnlyList<BitmapSource>> LoadFramesAsync(
        AnimationSource source,
        int width,
        int height,
        CancellationToken cancellationToken = default);
}
