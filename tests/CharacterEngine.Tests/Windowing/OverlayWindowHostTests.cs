namespace CharacterEngine.Tests.Windowing;

using System.Windows;
using System.Windows.Media;
using CharacterEngine.Tests.Fakes;
using CharacterEngine.Tests.TestHelpers;
using CharacterEngine.Windowing;
using MonkeyStack.Core.Character;

/// <summary>
/// A WPF ablakot nem jelenítjük meg (a tesztek képernyő nélkül is futnak), csak a beállításait ellenőrizzük. A
/// natív ablakstílusok (Alt-Tab, fókusz) és a kattintás-áteresztés csak futó alkalmazásban ellenőrizhető.
/// </summary>
public class OverlayWindowHostTests
{
    private static readonly ScreenRect Primary = new(0, 0, 1920, 1080);
    private static readonly ScreenRect LeftOfPrimary = new(-1920, 0, 0, 1080);

    [Fact]
    public void Constructor_MakesWindowTransparentBorderlessTopmostAndHidden()
    {
        StaTestHelper.RunInSta(() =>
        {
            var window = new Window();

            using var host = new OverlayWindowHost(window, new FakeScreenProvider());

            Assert.Equal(WindowStyle.None, window.WindowStyle);
            Assert.True(window.AllowsTransparency);
            Assert.Equal(Colors.Transparent, ((SolidColorBrush)window.Background).Color);
            Assert.True(window.Topmost);
            Assert.False(window.ShowInTaskbar);
            Assert.False(window.ShowActivated);
            Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
        });
    }

    [Fact]
    public void Constructor_PutsOwnCanvasIntoWindow()
    {
        StaTestHelper.RunInSta(() =>
        {
            var window = new Window();

            using var host = new OverlayWindowHost(window, new FakeScreenProvider());

            Assert.Same(host.Canvas, window.Content);
        });
    }

    [Fact]
    public void Layout_FromProviderScreens_CoversAllMonitorsAndShiftsCanvas()
    {
        StaTestHelper.RunInSta(() =>
        {
            var provider = new FakeScreenProvider();
            provider.SetScreens(Primary, LeftOfPrimary);
            var window = new Window();

            using var host = new OverlayWindowHost(window, provider);

            Assert.Equal((-1920.0, 0.0, 3840.0, 1080.0), (window.Left, window.Top, window.Width, window.Height));

            // A renderelő képernyő-koordinátában rajzol: a (0, 0) képernyőpont a Canvas (1920, 0) pontjára kerül.
            var offset = Assert.IsType<TranslateTransform>(host.Canvas.RenderTransform);
            Assert.Equal((1920.0, 0.0), (offset.X, offset.Y));
        });
    }

    [Fact]
    public void PlatformsChanged_NewMonitor_UpdatesLayout()
    {
        StaTestHelper.RunInSta(() =>
        {
            var provider = new FakeScreenProvider();
            provider.SetScreens(Primary);
            var window = new Window();
            using var host = new OverlayWindowHost(window, provider);

            provider.SetScreens(Primary, LeftOfPrimary); // monitort csatlakoztattak

            Assert.Equal(new ScreenRect(-1920, 0, 1920, 1080), host.Layout.Bounds);
            Assert.Equal(-1920.0, window.Left);
            Assert.Equal(1920.0, ((TranslateTransform)host.Canvas.RenderTransform).X);
        });
    }

    [Fact]
    public void NoScreenData_CoversWpfVirtualScreen()
    {
        StaTestHelper.RunInSta(() =>
        {
            var window = new Window();

            using var host = new OverlayWindowHost(window); // nincs platform-szolgáltató (pl. standalone futás)

            Assert.Equal(SystemParameters.VirtualScreenLeft, host.Layout.Bounds.Left);
            Assert.Equal(SystemParameters.VirtualScreenTop, host.Layout.Bounds.Top);
            Assert.Equal(SystemParameters.VirtualScreenWidth, host.Layout.Bounds.Width, 9);
            Assert.Equal(SystemParameters.VirtualScreenHeight, host.Layout.Bounds.Height, 9);
        });
    }

    [Fact]
    public void Dispose_UnsubscribesFromProvider()
    {
        StaTestHelper.RunInSta(() =>
        {
            var provider = new FakeScreenProvider();
            provider.SetScreens(Primary);
            var host = new OverlayWindowHost(new Window(), provider);
            Assert.Equal(1, provider.SubscriberCount);

            host.Dispose();

            Assert.Equal(0, provider.SubscriberCount);
        });
    }

    [Fact]
    public void GetInput_ComesFromGlobalMouseInput()
    {
        StaTestHelper.RunInSta(() =>
        {
            var source = new FakeNativeInputSource { CursorPosition = (640, 360), LeftButtonDown = true };

            using var host = new OverlayWindowHost(new Window(), new FakeScreenProvider(), new GlobalMouseInput(source));

            Assert.Equal((640.0, 360.0, true), host.GetInput());
        });
    }
}
