namespace CharacterEngine.Tests.Windowing;

using CharacterEngine.Tests.Fakes;
using CharacterEngine.Windowing;

public class GlobalMouseInputTests
{
    private const int Precision = 9;

    private readonly FakeNativeInputSource _source = new();
    private readonly GlobalMouseInput _input;

    public GlobalMouseInputTests()
    {
        _input = new GlobalMouseInput(_source);
    }

    // --- DIP-re váltás (ugyanaz a szabály, mint a platform-szolgáltatóban) ---

    [Fact]
    public void Unaware_ReturnsCoordinatesUnchanged()
    {
        // DPI-tudatlan folyamatnál a Windows már 96 DPI-re virtualizál: a kapott érték DIP. A rendszer DPI-je nem számít.
        _source.Awareness = DpiAwareness.Unaware;
        _source.SystemDpi = 144;
        _source.CursorPosition = (300, 150);

        var (x, y, _) = _input.GetInput();

        Assert.Equal(300.0, x);
        Assert.Equal(150.0, y);
    }

    [Fact]
    public void SystemAware_DividesBySystemScale()
    {
        _source.Awareness = DpiAwareness.SystemAware;
        _source.SystemDpi = 144; // 150 %
        _source.CursorPosition = (300, 150);

        var (x, y, _) = _input.GetInput();

        Assert.Equal(200.0, x, Precision);
        Assert.Equal(100.0, y, Precision);
    }

    [Fact]
    public void PerMonitorAware_UsesDpiOfMonitorUnderCursor()
    {
        // Két monitor: a bal 100 % (96 DPI), a jobb 200 % (192 DPI), a határ 1920 fizikai pixelnél.
        _source.Awareness = DpiAwareness.PerMonitorAware;
        _source.SystemDpi = 96;
        _source.MonitorDpiAtX = x => x < 1920 ? 96u : 192u;

        _source.CursorPosition = (1000, 400);
        var (leftX, leftY, _) = _input.GetInput();

        _source.CursorPosition = (3000, 400);
        var (rightX, rightY, _) = _input.GetInput();

        Assert.Equal(1000.0, leftX, Precision);
        Assert.Equal(400.0, leftY, Precision);
        Assert.Equal(1500.0, rightX, Precision);
        Assert.Equal(200.0, rightY, Precision);
    }

    [Fact]
    public void PerMonitorAware_MonitorDpiUnavailable_FallsBackToUnscaled()
    {
        // Mint a platform-szolgáltató: ha a monitor DPI-je nem kérdezhető le, 1,0-s váltószám.
        _source.Awareness = DpiAwareness.PerMonitorAware;
        _source.MonitorDpiAtX = _ => null;
        _source.CursorPosition = (500, 250);

        var (x, y, _) = _input.GetInput();

        Assert.Equal(500.0, x);
        Assert.Equal(250.0, y);
    }

    [Fact]
    public void CursorPositionUnavailable_ReturnsLastKnownPosition()
    {
        _source.Awareness = DpiAwareness.SystemAware;
        _source.SystemDpi = 192;
        _source.CursorPosition = (400, 200);
        _input.GetInput();

        _source.CursorPosition = null; // pl. zárolt asztal

        var (x, y, _) = _input.GetInput();

        Assert.Equal(200.0, x, Precision);
        Assert.Equal(100.0, y, Precision);
    }

    // --- Elsődleges egérgomb ---

    [Theory]
    [InlineData(false, true, false, true)]  // normál: a bal gomb számít
    [InlineData(false, false, true, false)] // normál: a jobb gomb nem ragad meg
    [InlineData(true, false, true, true)]   // balkezes: a jobb gomb számít
    [InlineData(true, true, false, false)]  // balkezes: a bal gomb nem ragad meg
    public void IsMouseDown_FollowsPrimaryButton(bool swapped, bool left, bool right, bool expected)
    {
        _source.ButtonsSwapped = swapped;
        _source.LeftButtonDown = left;
        _source.RightButtonDown = right;

        var (_, _, isMouseDown) = _input.GetInput();

        Assert.Equal(expected, isMouseDown);
    }

    [Fact]
    public void GetInput_MatchesRenderManagerInputProviderSignature()
    {
        // A RenderManager Func<(double, double, bool)> bemenet-szolgáltatót vár: metóduscsoportként átadható.
        Func<(double cursorX, double cursorY, bool isMouseDown)> provider = _input.GetInput;

        Assert.NotNull(provider);
    }
}
