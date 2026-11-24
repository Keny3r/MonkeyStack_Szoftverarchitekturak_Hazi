namespace CharacterEngine.Rendering;

using System.Windows.Controls;
using MonkeyStack.Core.Animation;

/// <summary>
/// A karakter és az overlay animációk kirajzolója az <see cref="AnimationView"/> példányokon keresztül.
/// Felelős az alsó-középső talppont átszámításáért bal-felső sarok koordinátára, a vízszintes tükrözésért,
/// a rétegsorrendért, valamint az inaktív nézetek elrejtéséért és felszabadításáért.
/// </summary>
/// <remarks>
/// A rétegsorrendet két saját al-Canvas biztosítja a cél Canvas-on: a <see cref="MovementLayer"/> mindig az
/// <see cref="OverlayLayer"/> alatt van, így az alap mozgásanimáció a ZIndex-étől függetlenül minden overlay alá kerül,
/// az overlay-ek pedig a saját rétegükön belül a ZIndex szerint rendeződnek.
/// </remarks>
public class CharacterRenderer : ICharacterRenderer
{
    private readonly Dictionary<IAnimation, AnimationView> _movementViews = new();
    private readonly Dictionary<IAnimation, AnimationView> _overlayViews = new();
    private Canvas? _targetCanvas;
    private Canvas? _movementLayer;
    private Canvas? _overlayLayer;

    // A rétegek WPF elemek, ezért első használatkor (a UI szálon) jönnek létre, nem a konstruktorban:
    // a renderelőt a RenderManager bármelyik szálon példányosíthatja.

    /// <summary>Az alap mozgásanimáció rétege (alul). Csak a UI szálon érhető el.</summary>
    public Canvas MovementLayer => _movementLayer ??= CreateLayer(zIndex: 0);

    /// <summary>Az overlay animációk rétege (felül). Csak a UI szálon érhető el.</summary>
    public Canvas OverlayLayer => _overlayLayer ??= CreateLayer(zIndex: 1);

    /// <summary>A mozgásanimációkhoz létrehozott nézetek.</summary>
    public IReadOnlyDictionary<IAnimation, AnimationView> MovementViews => _movementViews;

    /// <summary>Az overlay animációkhoz létrehozott nézetek.</summary>
    public IReadOnlyDictionary<IAnimation, AnimationView> OverlayViews => _overlayViews;

    private static Canvas CreateLayer(int zIndex)
    {
        var layer = new Canvas();
        Panel.SetZIndex(layer, zIndex);
        return layer;
    }

    public void Render(Canvas targetCanvas, CharacterRenderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(targetCanvas);
        ArgumentNullException.ThrowIfNull(snapshot);

        AttachLayers(targetCanvas);

        if (!snapshot.IsVisible)
        {
            HideAll();
            return;
        }

        var activeMovement = snapshot.MovementAnimation;
        if (activeMovement is not null)
        {
            Draw(_movementViews, MovementLayer, activeMovement, snapshot);
        }

        HideInactive(_movementViews, activeMovement is null ? [] : [activeMovement]);

        var activeOverlays = new HashSet<IAnimation>();
        foreach (var overlay in snapshot.OverlayAnimations ?? [])
        {
            if (overlay is null)
            {
                continue;
            }

            activeOverlays.Add(overlay);
            Draw(_overlayViews, OverlayLayer, overlay, snapshot);
        }

        HideInactive(_overlayViews, activeOverlays);
    }

    public void HideAll()
    {
        foreach (var view in _movementViews.Values.Concat(_overlayViews.Values))
        {
            view.Hide();
        }
    }

    public void DetachAll()
    {
        foreach (var view in _movementViews.Values.Concat(_overlayViews.Values))
        {
            view.Detach();
        }

        _movementViews.Clear();
        _overlayViews.Clear();

        if (_targetCanvas is not null)
        {
            _targetCanvas.Children.Remove(MovementLayer);
            _targetCanvas.Children.Remove(OverlayLayer);
            _targetCanvas = null;
        }
    }

    /// <summary>A rétegeket a cél Canvas-ra teszi; Canvas váltáskor előbb mindent leválaszt a régiről.</summary>
    private void AttachLayers(Canvas targetCanvas)
    {
        if (ReferenceEquals(_targetCanvas, targetCanvas))
        {
            return;
        }

        if (_targetCanvas is not null)
        {
            DetachAll();
        }

        targetCanvas.Children.Add(MovementLayer);
        targetCanvas.Children.Add(OverlayLayer);
        _targetCanvas = targetCanvas;
    }

    private static void Draw(
        Dictionary<IAnimation, AnimationView> views,
        Canvas layer,
        IAnimation animation,
        CharacterRenderSnapshot snapshot)
    {
        if (!views.TryGetValue(animation, out var view))
        {
            view = new AnimationView(animation, layer);
            views[animation] = view;
        }

        double topLeftX = snapshot.X - (animation.GetWidth() / 2.0);
        double topLeftY = snapshot.Y - animation.GetHeight();
        view.Draw(topLeftX, topLeftY, snapshot.IsFacingLeft);
    }

    private static void HideInactive(Dictionary<IAnimation, AnimationView> views, IReadOnlyCollection<IAnimation> active)
    {
        foreach (var (animation, view) in views)
        {
            if (!active.Contains(animation))
            {
                view.Hide();
            }
        }
    }
}
