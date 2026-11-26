namespace CharacterEngine.Tests.Physics;

using CharacterEngine.Physics;
using MonkeyStack.Core.Character;

public class PhysicsEngineTests
{
    private const int Precision = 9;

    // Kerek számok, hogy a várt értékek fejben is ellenőrizhetők legyenek.
    private static readonly PhysicsSettings TestSettings = PhysicsSettings.Default with
    {
        GravityPxPerSec2 = 1000.0,
        MaxFallSpeedPxPerSec = 500.0,
        SupportTolerancePx = 1.0,
    };

    private readonly PhysicsEngine _engine = new(TestSettings);

    // --- Gravitáció ---

    [Fact]
    public void ApplyGravity_FromRest_AcceleratesAndMovesDown()
    {
        var (y, velocity) = _engine.ApplyGravity(y: 0.0, velocityY: 0.0, TimeSpan.FromSeconds(0.1));

        Assert.Equal(100.0, velocity, Precision); // 0 + 1000 * 0.1
        Assert.Equal(10.0, y, Precision);         // 0 + 100 * 0.1
    }

    [Fact]
    public void ApplyGravity_CapsVelocityAtMaxFallSpeed()
    {
        var (y, velocity) = _engine.ApplyGravity(y: 0.0, velocityY: 490.0, TimeSpan.FromSeconds(0.1));

        Assert.Equal(500.0, velocity, Precision);
        Assert.Equal(50.0, y, Precision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ApplyGravity_NonPositiveDeltaTime_DoesNothing(int milliseconds)
    {
        var (y, velocity) = _engine.ApplyGravity(y: 42.0, velocityY: 7.0, TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal(42.0, y);
        Assert.Equal(7.0, velocity);
    }

    // --- Landolás ---

    [Fact]
    public void FindLandingPlatform_PlatformCrossedInStep_ReturnsIt()
    {
        var platform = new PlatformSegment(Y: 100, LeftX: 0, RightX: 200);

        var result = _engine.FindLandingPlatform(x: 50, previousY: 95, newY: 105, [platform]);

        Assert.Equal(platform, result);
    }

    [Fact]
    public void FindLandingPlatform_FastFallAcrossThinPlatform_StillLands()
    {
        // Egy lépésben 1000 px-t esik: ha csak az új pozíciót néznénk, átesne a peremen.
        var platform = new PlatformSegment(Y: 500, LeftX: 0, RightX: 200);

        var result = _engine.FindLandingPlatform(x: 50, previousY: 0, newY: 1000, [platform]);

        Assert.Equal(platform, result);
    }

    [Fact]
    public void FindLandingPlatform_SeveralCrossed_ReturnsTopmost()
    {
        var lower = new PlatformSegment(Y: 400, LeftX: 0, RightX: 200);
        var upper = new PlatformSegment(Y: 200, LeftX: 0, RightX: 200);

        var result = _engine.FindLandingPlatform(x: 50, previousY: 0, newY: 1000, [lower, upper]);

        Assert.Equal(upper, result);
    }

    [Theory]
    [InlineData(0.0)]   // bal szél
    [InlineData(200.0)] // jobb szél
    public void FindLandingPlatform_OnPlatformEdge_Lands(double x)
    {
        var platform = new PlatformSegment(Y: 100, LeftX: 0, RightX: 200);

        Assert.Equal(platform, _engine.FindLandingPlatform(x, previousY: 90, newY: 110, [platform]));
    }

    [Theory]
    [InlineData(-0.5, 90.0, 110.0)] // a platformtól balra
    [InlineData(250.0, 90.0, 110.0)] // a platformtól jobbra
    [InlineData(50.0, 80.0, 99.0)]   // még nem érte el a platformot
    [InlineData(50.0, 110.0, 90.0)]  // felfelé mozog (pl. húzás közben)
    public void FindLandingPlatform_NoLanding_ReturnsNull(double x, double previousY, double newY)
    {
        var platform = new PlatformSegment(Y: 100, LeftX: 0, RightX: 200);

        Assert.Null(_engine.FindLandingPlatform(x, previousY, newY, [platform]));
    }

    // --- Alátámasztás ---

    [Theory]
    [InlineData(100.0)]
    [InlineData(99.0)]
    [InlineData(101.0)]
    public void FindSupportingPlatform_WithinTolerance_ReturnsPlatform(double y)
    {
        var platform = new PlatformSegment(Y: 100, LeftX: 0, RightX: 200);

        Assert.Equal(platform, _engine.FindSupportingPlatform(x: 50, y, [platform]));
    }

    [Fact]
    public void FindSupportingPlatform_WindowMovedBeyondTolerance_ReturnsNull()
    {
        // Az ablakot 5 px-szel lejjebb húzták: a karakternek le kell esnie (a #3 döntése).
        var moved = new PlatformSegment(Y: 105, LeftX: 0, RightX: 200);

        Assert.Null(_engine.FindSupportingPlatform(x: 50, y: 100, [moved]));
    }

    [Fact]
    public void FindSupportingPlatform_NearEdge_IsStillSupported()
    {
        // A szél közelében landolt karakter ne essen le azonnal: itt nem számít a PlatformEdgeMargin.
        var platform = new PlatformSegment(Y: 100, LeftX: 0, RightX: 200);

        Assert.Equal(platform, _engine.FindSupportingPlatform(x: 1, y: 100, [platform]));
    }

    [Fact]
    public void FindSupportingPlatform_OutsideX_ReturnsNull()
    {
        var platform = new PlatformSegment(Y: 100, LeftX: 0, RightX: 200);

        Assert.Null(_engine.FindSupportingPlatform(x: 201, y: 100, [platform]));
        Assert.Null(_engine.FindSupportingPlatform(x: 50, y: 100, []));
    }

    [Fact]
    public void FindSupportingPlatform_SeveralCandidates_ReturnsClosest()
    {
        var slightlyAbove = new PlatformSegment(Y: 99.2, LeftX: 0, RightX: 200);
        var exact = new PlatformSegment(Y: 100, LeftX: 0, RightX: 200);

        Assert.Equal(exact, _engine.FindSupportingPlatform(x: 50, y: 100, [slightlyAbove, exact]));
    }

    // --- Találatvizsgálat (megragadás) ---
    // Talppont: (100, 200), méret 40 x 60 => téglalap: X 80..120, Y 140..200.

    [Theory]
    [InlineData(100.0, 170.0)] // közép
    [InlineData(80.0, 140.0)]  // bal-felső sarok
    [InlineData(120.0, 200.0)] // jobb-alsó sarok (talp)
    public void HitTest_InsideOrOnEdge_ReturnsTrue(double cursorX, double cursorY)
    {
        Assert.True(_engine.HitTest(x: 100, y: 200, width: 40, height: 60, cursorX, cursorY));
    }

    [Theory]
    [InlineData(79.0, 170.0)]  // balra
    [InlineData(121.0, 170.0)] // jobbra
    [InlineData(100.0, 139.0)] // fölötte
    [InlineData(100.0, 201.0)] // alatta
    public void HitTest_Outside_ReturnsFalse(double cursorX, double cursorY)
    {
        Assert.False(_engine.HitTest(x: 100, y: 200, width: 40, height: 60, cursorX, cursorY));
    }

    [Fact]
    public void HitTest_ZeroSize_ReturnsFalse()
    {
        Assert.False(_engine.HitTest(x: 100, y: 200, width: 0, height: 60, cursorX: 100, cursorY: 200));
    }
}
