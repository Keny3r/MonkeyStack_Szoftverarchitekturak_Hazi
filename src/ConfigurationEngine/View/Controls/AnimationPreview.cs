namespace ConfigurationEngine.View.Controls;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonkeyStack.Core.Animation;

/// <summary>
/// Egy sor élő előnézete (gui.md, "Kirajzolás"): saját <see cref="Canvas"/>-ra, saját <see cref="AnimationView"/>
/// példánnyal, a <c>(0, 0)</c> pontba, tükrözés nélkül rajzol (<c>Draw(0, 0)</c>). A rajzolási ciklusban
/// (<see cref="CompositionTarget.Rendering"/>) csak kirajzol, a léptetést a motor végzi; csak akkor rajzol, ha látható
/// (az aktuális fülön van). A listák soraiban kicsinyítve (<see cref="IsCompact"/>, <see cref="Viewbox"/>), a részletek
/// panelen teljes karakterméretben látszik. Betöltés közben „betöltés…”, hibánál a hiba üzenete jelenik meg.
/// </summary>
public sealed class AnimationPreview : Border
{
    public static readonly DependencyProperty PreviewProperty = DependencyProperty.Register(
        nameof(Preview),
        typeof(PreviewViewModel),
        typeof(AnimationPreview),
        new PropertyMetadata(null, (d, e) => ((AnimationPreview)d).OnPreviewChanged(
            (PreviewViewModel?)e.OldValue,
            (PreviewViewModel?)e.NewValue)));

    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact),
        typeof(bool),
        typeof(AnimationPreview),
        new PropertyMetadata(false, (d, _) => ((AnimationPreview)d).UpdateLayoutMode()));

    public static readonly DependencyProperty CompactSizeProperty = DependencyProperty.Register(
        nameof(CompactSize),
        typeof(double),
        typeof(AnimationPreview),
        new PropertyMetadata(48.0, (d, _) => ((AnimationPreview)d).UpdateLayoutMode()));

    private static readonly Brush ErrorBrush = CreateFrozenBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));

    private readonly Canvas _canvas = new() { ClipToBounds = true };
    private readonly Viewbox _viewbox = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _status = new()
    {
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 11
    };

    private readonly Grid _root = new();
    private AnimationView? _view;
    private IAnimation? _drawn;
    private bool _isRendering;

    public AnimationPreview()
    {
        _root.Children.Add(_status);
        Child = _root;
        UpdateLayoutMode();

        Loaded += (_, _) => UpdateRendering();
        Unloaded += (_, _) =>
        {
            UpdateRendering();
            DetachView();
        };
        IsVisibleChanged += (_, _) => UpdateRendering();
    }

    /// <summary>A sor előnézete.</summary>
    public PreviewViewModel? Preview
    {
        get => (PreviewViewModel?)GetValue(PreviewProperty);
        set => SetValue(PreviewProperty, value);
    }

    /// <summary>Kicsinyített megjelenítés a listák soraiban (<see cref="CompactSize"/> × <see cref="CompactSize"/>).</summary>
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public double CompactSize
    {
        get => (double)GetValue(CompactSizeProperty);
        set => SetValue(CompactSizeProperty, value);
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void OnPreviewChanged(PreviewViewModel? oldPreview, PreviewViewModel? newPreview)
    {
        if (oldPreview is not null)
        {
            oldPreview.PropertyChanged -= OnPreviewPropertyChanged;
        }

        if (newPreview is not null)
        {
            newPreview.PropertyChanged += OnPreviewPropertyChanged;
        }

        DetachView();
        UpdateStatus();
        if (_isRendering)
        {
            Draw();
        }
    }

    private void OnPreviewPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateStatus();

    private void UpdateStatus()
    {
        var preview = Preview;
        if (preview is { HasError: true })
        {
            _status.Text = preview.ErrorMessage;
            _status.Foreground = ErrorBrush;
            _status.Visibility = Visibility.Visible;
            ToolTip = preview.ErrorMessage;
        }
        else if (preview is { IsLoading: true })
        {
            _status.Text = "betöltés…";
            _status.ClearValue(TextBlock.ForegroundProperty);
            _status.Visibility = Visibility.Visible;
            ToolTip = null;
        }
        else
        {
            _status.Visibility = Visibility.Collapsed;
            ToolTip = null;
        }
    }

    private void UpdateLayoutMode()
    {
        _root.Children.Remove(_viewbox);
        _root.Children.Remove(_canvas);
        _viewbox.Child = null;

        if (IsCompact)
        {
            _viewbox.Child = _canvas;
            _root.Children.Insert(0, _viewbox);
            Width = CompactSize;
            Height = CompactSize;
        }
        else
        {
            _root.Children.Insert(0, _canvas);
            ClearValue(WidthProperty);
            ClearValue(HeightProperty);
        }
    }

    private void UpdateRendering()
    {
        var shouldRender = IsLoaded && IsVisible;
        if (shouldRender == _isRendering)
        {
            return;
        }

        _isRendering = shouldRender;
        if (shouldRender)
        {
            CompositionTarget.Rendering += OnRendering;
            Draw();
        }
        else
        {
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void OnRendering(object? sender, EventArgs e) => Draw();

    /// <summary>Csak kirajzol: az animáció aktuális képkockáját mutatja, nem lépteti és nem módosítja.</summary>
    private void Draw()
    {
        var animation = Preview?.Animation;
        if (!ReferenceEquals(animation, _drawn))
        {
            DetachView();
            if (animation is not null)
            {
                _view = new AnimationView(animation, _canvas);
                _drawn = animation;
            }
        }

        if (_view is null || animation is null)
        {
            return;
        }

        _canvas.Width = animation.GetWidth();
        _canvas.Height = animation.GetHeight();
        _view.Draw(0, 0);
    }

    private void DetachView()
    {
        _view?.Detach();
        _view = null;
        _drawn = null;
    }
}
