namespace CharacterEngine.Tests.Windowing;

using CharacterEngine.Windowing;
using MonkeyStack.Core.Character;

public class OverlayLayoutTests
{
    private static ScreenArea Screen(double left, double top, double right, double bottom) =>
        new(new ScreenRect(left, top, right, bottom), new ScreenRect(left, top, right, bottom - 40), HasFullscreenWindow: false);

    [Fact]
    public void FromScreens_NoScreens_ReturnsNull()
    {
        Assert.Null(OverlayLayout.FromScreens([]));
    }

    [Fact]
    public void FromScreens_SingleMonitor_CoversItWithZeroOffset()
    {
        var layout = OverlayLayout.FromScreens([Screen(0, 0, 1920, 1080)]);

        Assert.Equal(new ScreenRect(0, 0, 1920, 1080), layout!.Value.Bounds);
        Assert.Equal((100.0, 200.0), layout.Value.ToCanvas(100, 200));
    }

    [Fact]
    public void FromScreens_MonitorLeftOfPrimary_HasNegativeOrigin()
    {
        // A fő monitortól balra lévő monitor negatív X-en kezdődik: a Canvas 0 pontja ott van.
        var layout = OverlayLayout.FromScreens([Screen(0, 0, 1920, 1080), Screen(-1920, 0, 0, 1080)])!.Value;

        Assert.Equal(new ScreenRect(-1920, 0, 1920, 1080), layout.Bounds);
        Assert.Equal((1920.0 + 100.0, 200.0), layout.ToCanvas(100, 200));   // a fő monitor pontja a Canvas jobb felén
        Assert.Equal((0.0, 0.0), layout.ToCanvas(-1920, 0));                // a bal monitor sarka a Canvas sarka
    }

    [Fact]
    public void FromScreens_MonitorsWithDifferentHeightsAndOneAbove_CoversAll()
    {
        // Egy alacsonyabb monitor jobbra, egy pedig a fő monitor fölött: a befoglaló téglalap mindhármat lefedi.
        var layout = OverlayLayout.FromScreens(
        [
            Screen(0, 0, 1920, 1080),
            Screen(1920, 200, 3200, 1000),
            Screen(0, -1080, 1920, 0),
        ])!.Value;

        Assert.Equal(new ScreenRect(0, -1080, 3200, 1080), layout.Bounds);
    }

    [Fact]
    public void ToScreen_IsInverseOfToCanvas()
    {
        var layout = OverlayLayout.FromScreens([Screen(-1920, -300, 0, 780), Screen(0, 0, 2560, 1440)])!.Value;

        var (canvasX, canvasY) = layout.ToCanvas(1234.5, -42.25);
        var (screenX, screenY) = layout.ToScreen(canvasX, canvasY);

        Assert.Equal(1234.5, screenX);
        Assert.Equal(-42.25, screenY);
    }
}
