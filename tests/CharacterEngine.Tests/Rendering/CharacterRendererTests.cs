namespace CharacterEngine.Tests.Rendering;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CharacterEngine.Rendering;
using CharacterEngine.Tests.Fakes;
using CharacterEngine.Tests.TestHelpers;

public class CharacterRendererTests
{
    private static CharacterRenderSnapshot Snapshot(
        FakeAnimation? movement,
        IReadOnlyList<FakeAnimation>? overlays = null,
        double x = 200,
        double y = 300,
        bool facingLeft = false,
        bool visible = true) =>
        new(x, y, facingLeft, movement, overlays ?? [], visible);

    private static Image SingleImage(Canvas layer) => Assert.IsType<Image>(Assert.Single(layer.Children));

    [Fact]
    public void Render_AddsMovementLayerBelowOverlayLayer()
    {
        StaTestHelper.RunInSta(() =>
        {
            var renderer = new CharacterRenderer();
            var canvas = new Canvas();

            renderer.Render(canvas, Snapshot(new FakeAnimation()));

            Assert.Equal(2, canvas.Children.Count);
            Assert.Contains(renderer.MovementLayer, canvas.Children.Cast<UIElement>());
            Assert.Contains(renderer.OverlayLayer, canvas.Children.Cast<UIElement>());
            Assert.True(Panel.GetZIndex(renderer.MovementLayer) < Panel.GetZIndex(renderer.OverlayLayer));
        });
    }

    [Fact]
    public void Render_DrawsMovementAndOverlayAtBottomCenterPivot()
    {
        StaTestHelper.RunInSta(() =>
        {
            var renderer = new CharacterRenderer();
            var canvas = new Canvas();

            // Talppont (200, 300): mozgás 80x60 -> (160, 240); overlay 40x40 -> (180, 260)
            renderer.Render(canvas, Snapshot(new FakeAnimation(80, 60), [new FakeAnimation(40, 40, zIndex: 10)]));

            var movementImg = SingleImage(renderer.MovementLayer);
            var overlayImg = SingleImage(renderer.OverlayLayer);

            Assert.Equal(160.0, Canvas.GetLeft(movementImg));
            Assert.Equal(240.0, Canvas.GetTop(movementImg));
            Assert.Equal(180.0, Canvas.GetLeft(overlayImg));
            Assert.Equal(260.0, Canvas.GetTop(overlayImg));
            Assert.Equal(10, Panel.GetZIndex(overlayImg));
        });
    }

    [Fact]
    public void Render_MovementStaysInMovementLayer_EvenWithHighZIndex()
    {
        StaTestHelper.RunInSta(() =>
        {
            var renderer = new CharacterRenderer();
            var canvas = new Canvas();

            renderer.Render(canvas, Snapshot(new FakeAnimation(zIndex: 50), [new FakeAnimation(zIndex: 0)]));

            Assert.Single(renderer.MovementLayer.Children);
            Assert.Single(renderer.OverlayLayer.Children);
        });
    }

    [Fact]
    public void Render_SameAnimationAsMovementAndOverlay_GetsSeparateViews()
    {
        StaTestHelper.RunInSta(() =>
        {
            var renderer = new CharacterRenderer();
            var canvas = new Canvas();
            var shared = new FakeAnimation();

            renderer.Render(canvas, Snapshot(shared, [shared]));

            Assert.Single(renderer.MovementViews);
            Assert.Single(renderer.OverlayViews);
            Assert.Single(renderer.MovementLayer.Children);
            Assert.Single(renderer.OverlayLayer.Children);
        });
    }

    [Fact]
    public void Render_WhenFacingLeft_FlipsHorizontally()
    {
        StaTestHelper.RunInSta(() =>
        {
            var renderer = new CharacterRenderer();
            var canvas = new Canvas();

            renderer.Render(canvas, Snapshot(new FakeAnimation(), [new FakeAnimation()], facingLeft: true));

            Assert.Equal(-1.0, Assert.IsType<ScaleTransform>(SingleImage(renderer.MovementLayer).RenderTransform).ScaleX);
            Assert.Equal(-1.0, Assert.IsType<ScaleTransform>(SingleImage(renderer.OverlayLayer).RenderTransform).ScaleX);
        });
    }

    [Fact]
    public void Render_WhenNotVisible_HidesAllAnimations()
    {
        StaTestHelper.RunInSta(() =>
        {
            var renderer = new CharacterRenderer();
            var canvas = new Canvas();
            var snapshot = Snapshot(new FakeAnimation());

            renderer.Render(canvas, snapshot);
            var img = SingleImage(renderer.MovementLayer);
            Assert.Equal(Visibility.Visible, img.Visibility);

            renderer.Render(canvas, snapshot with { IsVisible = false });
            Assert.Equal(Visibility.Collapsed, img.Visibility);
        });
    }

    [Fact]
    public void Render_HidesAnimationsNoLongerInSnapshot()
    {
        StaTestHelper.RunInSta(() =>
        {
            var renderer = new CharacterRenderer();
            var canvas = new Canvas();
            var movement = new FakeAnimation();
            var overlay1 = new FakeAnimation(zIndex: 1);
            var overlay2 = new FakeAnimation(zIndex: 2);

            renderer.Render(canvas, Snapshot(movement, [overlay1, overlay2]));
            var overlay2Img = Assert.IsType<Image>(renderer.OverlayLayer.Children[1]);
            Assert.Equal(Visibility.Visible, overlay2Img.Visibility);

            renderer.Render(canvas, Snapshot(movement, [overlay1]));
            Assert.Equal(Visibility.Collapsed, overlay2Img.Visibility);
        });
    }

    [Fact]
    public void Render_HidesPreviousMovementAnimation_OnMovementChange()
    {
        StaTestHelper.RunInSta(() =>
        {
            var renderer = new CharacterRenderer();
            var canvas = new Canvas();

            renderer.Render(canvas, Snapshot(new FakeAnimation()));
            var oldImg = SingleImage(renderer.MovementLayer);

            renderer.Render(canvas, Snapshot(new FakeAnimation()));

            Assert.Equal(Visibility.Collapsed, oldImg.Visibility);
        });
    }

    [Fact]
    public void Render_OnNewCanvas_MovesLayersFromOldCanvas()
    {
        StaTestHelper.RunInSta(() =>
        {
            var renderer = new CharacterRenderer();
            var oldCanvas = new Canvas();
            var newCanvas = new Canvas();
            var snapshot = Snapshot(new FakeAnimation());

            renderer.Render(oldCanvas, snapshot);
            renderer.Render(newCanvas, snapshot);

            Assert.Empty(oldCanvas.Children);
            Assert.Equal(2, newCanvas.Children.Count);
            Assert.Single(renderer.MovementLayer.Children);
        });
    }

    [Fact]
    public void DetachAll_RemovesLayersAndClearsViews()
    {
        StaTestHelper.RunInSta(() =>
        {
            var renderer = new CharacterRenderer();
            var canvas = new Canvas();

            renderer.Render(canvas, Snapshot(new FakeAnimation(), [new FakeAnimation()]));
            renderer.DetachAll();

            Assert.Empty(canvas.Children);
            Assert.Empty(renderer.MovementLayer.Children);
            Assert.Empty(renderer.OverlayLayer.Children);
            Assert.Empty(renderer.MovementViews);
            Assert.Empty(renderer.OverlayViews);
        });
    }
}
