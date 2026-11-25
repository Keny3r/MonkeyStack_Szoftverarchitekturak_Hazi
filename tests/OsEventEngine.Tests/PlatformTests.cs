namespace OsEventEngine.Tests;

using Microsoft.Extensions.Time.Testing;
using MonkeyStack.Core.Character;
using OsEventEngine.Native;
using OsEventEngine.Notifications;
using OsEventEngine.Platforms;

/// <summary>
/// A platform-számítás (platform_provider_proposal.md, 3. fejezet) és a szolgáltató frissítési logikája, natív hívás nélkül.
/// </summary>
public class PlatformTests
{
    // 1920×1080-as monitor, alul 40 px-es tálca.
    private static readonly SceneMonitor Main = new(new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 1.0);

    private static SceneWindow Window(double left, double top, double right, double bottom, bool maximized = false) =>
        new(new ScreenRect(left, top, right, bottom), maximized);

    private static IReadOnlyList<PlatformSegment> Platforms(params SceneWindow[] windows) =>
        PlatformCalculator.Calculate(new DesktopScene(windows, [Main])).Platforms;

    [Fact]
    public void WindowTopEdgeAndTaskbarAreWalkable()
    {
        var platforms = Platforms(Window(100, 200, 900, 700));

        Assert.Equal([new PlatformSegment(200, 100, 900), new PlatformSegment(1040, 0, 1920)], platforms);
    }

    [Fact]
    public void WindowInFrontCutsTheEdgeBehindIt()
    {
        var platforms = Platforms(
            Window(400, 100, 600, 500), // elöl
            Window(100, 300, 900, 700)); // mögötte, a felső éle 300-on: a 400–600 rész takarva

        Assert.Equal(
            [
                new PlatformSegment(100, 400, 600),
                new PlatformSegment(300, 100, 400),
                new PlatformSegment(300, 600, 900),
                new PlatformSegment(1040, 0, 1920),
            ],
            platforms);
    }

    [Fact]
    public void WindowBehindDoesNotCutTheEdgeInFront()
    {
        var platforms = Platforms(
            Window(100, 300, 900, 700), // elöl
            Window(400, 100, 600, 500)); // mögötte: a felső éle 100-on szabad, és az elülső élét sem takarja

        Assert.Contains(new PlatformSegment(300, 100, 900), platforms);
        Assert.Contains(new PlatformSegment(100, 400, 600), platforms);
    }

    [Fact]
    public void MaximizedWindowHasNoEdgeAndHidesEverythingBehind()
    {
        var platforms = Platforms(
            Window(0, 0, 1920, 1040, maximized: true),
            Window(100, 200, 900, 700));

        // A karakter a tálcára esik.
        Assert.Equal([new PlatformSegment(1040, 0, 1920)], platforms);
    }

    [Fact]
    public void FullscreenWindowMovesTheGroundToTheMonitorBottom()
    {
        var (platforms, screens) = PlatformCalculator.Calculate(new DesktopScene(
            [Window(0, 0, 1920, 1080), Window(100, 200, 900, 700)],
            [Main]));

        Assert.Equal([new PlatformSegment(1080, 0, 1920)], platforms);
        Assert.True(Assert.Single(screens).HasFullscreenWindow);
    }

    [Fact]
    public void WindowAtTheTopOfTheWorkAreaHasNoEdge()
    {
        var platforms = Platforms(Window(100, 0, 900, 700));

        Assert.Equal([new PlatformSegment(1040, 0, 1920)], platforms);
    }

    [Fact]
    public void SegmentsNarrowerThanTheMinimumAreDropped()
    {
        var platforms = Platforms(
            Window(110, 100, 890, 500), // elöl: a mögötte lévő élből csak 2 × 10 px marad
            Window(100, 300, 900, 700));

        Assert.DoesNotContain(platforms, p => p.Y == 300);
    }

    [Fact]
    public void EdgesBelowTheTaskbarOrOffScreenAreDropped()
    {
        var platforms = Platforms(
            Window(100, 1050, 900, 1300), // a tálca mögött
            Window(-500, 300, 300, 700)); // részben a képernyőn kívül: a látható részre vágva

        Assert.Equal([new PlatformSegment(300, 0, 300), new PlatformSegment(1040, 0, 1920)], platforms);
    }

    [Fact]
    public void CoordinatesAreConvertedToDipPerMonitor()
    {
        // 150%-os skálázás: 2880×1620 fizikai pixel = 1920×1080 DIP.
        var monitor = new SceneMonitor(new(0, 0, 2880, 1620), new(0, 0, 2880, 1560), 1.5);

        var (platforms, screens) = PlatformCalculator.Calculate(new DesktopScene([Window(150, 300, 1350, 900)], [monitor]));

        Assert.Equal([new PlatformSegment(200, 100, 900), new PlatformSegment(1040, 0, 1920)], platforms);
        Assert.Equal(new ScreenRect(0, 0, 1920, 1080), Assert.Single(screens).Bounds);
    }

    [Fact]
    public void EdgeSpanningTwoMonitorsIsSplitAtTheBorder()
    {
        var right = new SceneMonitor(new(1920, 0, 3840, 1080), new(1920, 0, 3840, 1080), 1.0);

        var (platforms, screens) = PlatformCalculator.Calculate(new DesktopScene([Window(1500, 400, 2500, 800)], [Main, right]));

        Assert.Contains(new PlatformSegment(400, 1500, 1920), platforms);
        Assert.Contains(new PlatformSegment(400, 1920, 2500), platforms);
        Assert.Contains(new PlatformSegment(1080, 1920, 3840), platforms); // tálca nélküli monitor: a képernyő alja
        Assert.Equal(2, screens.Count);
    }

    [Fact]
    public void ProviderComputesTheFirstSnapshotOnStart()
    {
        var scanner = new FakeScanner(new DesktopScene([Window(100, 200, 900, 700)], [Main]));
        using var provider = new Win32PlatformProvider(scanner, notifications: null, new FakeTimeProvider());

        Assert.Same(PlatformSnapshot.Empty, provider.Current);
        provider.Start();

        Assert.Equal(1, provider.Current.Version);
        Assert.Equal(2, provider.Current.Platforms.Count);
    }

    [Fact]
    public void ProviderRaisesChangedOnlyWhenPlatformsChange()
    {
        var scanner = new FakeScanner(new DesktopScene([Window(100, 200, 900, 700)], [Main]));
        using var provider = new Win32PlatformProvider(scanner, notifications: null, new FakeTimeProvider());
        var changes = new List<PlatformSnapshot>();
        provider.PlatformsChanged += (_, s) => changes.Add(s);
        provider.Start();

        provider.Refresh();
        scanner.Scene = new DesktopScene([Window(100, 250, 900, 700)], [Main]);
        provider.Refresh();

        Assert.Equal([1L, 2L], changes.Select(c => c.Version));
        Assert.Contains(new PlatformSegment(250, 100, 900), provider.Current.Platforms);
    }

    [Fact]
    public void ProviderKeepsTheLastSnapshotWhenScanningFails()
    {
        var scanner = new FakeScanner(new DesktopScene([Window(100, 200, 900, 700)], [Main]));
        using var provider = new Win32PlatformProvider(scanner, notifications: null, new FakeTimeProvider());
        provider.Start();
        var before = provider.Current;

        scanner.Fail = true;
        provider.Refresh();

        Assert.Same(before, provider.Current);
    }

    [Fact]
    public async Task ProviderRecalculatesWhenTheReactorReportsAMove()
    {
        var scanner = new FakeScanner(new DesktopScene([Window(100, 200, 900, 700)], [Main]));
        var notifications = new FakeNotifications();
        using var provider = new Win32PlatformProvider(scanner, notifications, new FakeTimeProvider());
        provider.Start();

        scanner.Scene = new DesktopScene([Window(300, 400, 900, 700)], [Main]);
        notifications.Raise(OsNotification.WindowMoved);

        Assert.True(await Eventually.UntilAsync(() => provider.Current.Version == 2));
        Assert.Contains(new PlatformSegment(400, 300, 900), provider.Current.Platforms);
    }

    [Fact]
    public void ProviderUnregistersFromTheReactorOnDispose()
    {
        var notifications = new FakeNotifications();
        var provider = new Win32PlatformProvider(new FakeScanner(new DesktopScene([], [Main])), notifications, new FakeTimeProvider());
        provider.Start();
        Assert.True(notifications.Count(OsNotification.WindowMoved) > 0);

        provider.Dispose();

        Assert.Equal(0, notifications.Count(OsNotification.WindowMoved));
    }

    private sealed class FakeScanner(DesktopScene scene) : IDesktopScanner
    {
        public DesktopScene Scene { get; set; } = scene;

        public bool Fail { get; set; }

        public DesktopScene Scan() => Fail ? throw new InvalidOperationException("scan") : Scene;
    }
}

/// <summary>Hamis Reactor: a regisztrált handlereket kézzel lehet kiváltani.</summary>
internal sealed class FakeNotifications : IOsNotificationSource
{
    private readonly object _sync = new();
    private readonly List<(OsNotification Kind, Action Handler)> _handlers = [];

    public IDisposable Register(OsNotification notification, Action handler)
    {
        var entry = (notification, handler);
        lock (_sync)
        {
            _handlers.Add(entry);
        }

        return new Unsubscribe(() =>
        {
            lock (_sync)
            {
                _handlers.Remove(entry);
            }
        });
    }

    public int Count(OsNotification notification)
    {
        lock (_sync)
        {
            return _handlers.Count(h => h.Kind == notification);
        }
    }

    public void Raise(OsNotification notification)
    {
        Action[] handlers;
        lock (_sync)
        {
            handlers = _handlers.Where(h => h.Kind == notification).Select(h => h.Handler).ToArray();
        }

        foreach (var handler in handlers)
        {
            handler();
        }
    }

    private sealed class Unsubscribe(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
