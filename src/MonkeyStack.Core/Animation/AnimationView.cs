namespace MonkeyStack.Core.Animation;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

/// <summary>
/// A View réteghez tartozó animáció-megjelenítő osztály.
/// Ugyanannak az IAnimation modellnek egy AnimationView példánya a Config menüben,
/// egy másik példánya pedig a Karakter View-ban tudja kirenderelni az aktuális képkockát
/// a saját Canvas-ára a Draw(x, y, isFlipped) hívással.
/// </summary>
public class AnimationView
{
    private readonly Canvas _targetCanvas;
    private readonly Image _imageElement;
    private readonly ScaleTransform _scaleTransform;

    public IAnimation Animation { get; set; }

    public AnimationView(IAnimation animation, Canvas targetCanvas)
    {
        Animation = animation ?? throw new ArgumentNullException(nameof(animation));
        _targetCanvas = targetCanvas ?? throw new ArgumentNullException(nameof(targetCanvas));

        _scaleTransform = new ScaleTransform(1.0, 1.0);
        _imageElement = new Image
        {
            Stretch = Stretch.Fill,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _scaleTransform
        };
    }

    /// <summary>
    /// Kirajzolja az animáció aktuális képkockáját a cél Canvas (x, y) bal-felső koordinátájára,
    /// a modellből lekért (GetWidth, GetHeight) mérettel és opcionális vízszintes tükrözéssel.
    /// </summary>
    /// <param name="x">A kirajzolás bal felső X koordinátája a Canvas-on.</param>
    /// <param name="y">A kirajzolás bal felső Y koordinátája a Canvas-on.</param>
    /// <param name="isFlipped">Ha igaz, a képkockát vízszintesen tükrözi (pl. amikor a karakter megfordul).</param>
    public void Draw(double x, double y, bool isFlipped = false)
    {
        if (!_targetCanvas.Children.Contains(_imageElement))
        {
            _targetCanvas.Children.Add(_imageElement);
        }

        _imageElement.Visibility = Visibility.Visible;
        _imageElement.Source = Animation.CurrentFrame;
        _imageElement.Width = Animation.GetWidth();
        _imageElement.Height = Animation.GetHeight();
        _scaleTransform.ScaleX = isFlipped ? -1.0 : 1.0;

        Canvas.SetLeft(_imageElement, x);
        Canvas.SetTop(_imageElement, y);
        Panel.SetZIndex(_imageElement, Animation.ZIndex);
    }

    /// <summary>
    /// Elrejti az animációt a Canvas-on (pl. ha egy overlay már nem aktív).
    /// </summary>
    public void Hide()
    {
        _imageElement.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Eltávolítja a vizuális elemet a cél Canvas-ról.
    /// </summary>
    public void Detach()
    {
        if (_targetCanvas.Children.Contains(_imageElement))
        {
            _targetCanvas.Children.Remove(_imageElement);
        }
    }
}
