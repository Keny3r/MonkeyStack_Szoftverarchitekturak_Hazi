namespace CharacterEngine.Windowing;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MonkeyStack.Core.Character;

/// <summary>
/// Az átlátszó, keret nélküli, mindig legfelül lévő overlay ablak kezelője (Wrapper Facade minta): a kapott WPF ablakot
/// az összes monitort lefedő, a tálcán és az Alt-Tab listában nem szereplő, a fókuszt el nem vevő ablakká állítja, és
/// a monitorok változását követi.
/// </summary>
/// <remarks>
/// <para>
/// A host birtokolja az ablak tartalmát: egy saját <see cref="Canvas"/>-t tesz bele, amelyre a renderelő rajzol. A Canvasra
/// az ablak helyéből adódó eltolást (<see cref="TranslateTransform"/>) teszi, így a renderelő, a fizika és az egér is
/// képernyő-koordinátában dolgozhat (lásd <see cref="OverlayLayout"/>).
/// </para>
/// <para>
/// Kattintás-áteresztés: az átlátszó (<c>AllowsTransparency</c>) WPF ablak rétegzett ablak, amelyen a teljesen átlátszó
/// képpontok a Windowsban átengedik a kattintást, így a karakteren kívül az alatta lévő ablakok kapják meg. Ezt futó
/// alkalmazásban kell ellenőrizni.
/// </para>
/// </remarks>
public sealed class OverlayWindowHost : IOverlayWindowHost
{
    private readonly Window _window;
    private readonly IPlatformProvider? _platformProvider;
    private readonly GlobalMouseInput _mouseInput;
    private readonly TranslateTransform _canvasOffset = new();
    private bool _disposed;

    /// <param name="window">A beállítandó ablak. Még nem lehet megjelenítve, mert az átlátszóság csak megjelenítés előtt állítható.</param>
    /// <param name="platformProvider">A monitorok forrása; ha nincs, a WPF virtuális képernyőjét fedi le.</param>
    /// <param name="mouseInput">Az egérbemenet; alapértelmezetten a Win32-es globális lekérdezés.</param>
    public OverlayWindowHost(Window window, IPlatformProvider? platformProvider = null, GlobalMouseInput? mouseInput = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        if (PresentationSource.FromVisual(window) is not null)
        {
            throw new InvalidOperationException("Az overlay ablakot a megjelenítése előtt kell a hostnak átadni.");
        }

        _platformProvider = platformProvider;
        _mouseInput = mouseInput ?? new GlobalMouseInput();

        Canvas = new Canvas { RenderTransform = _canvasOffset };
        ConfigureWindow();
        ApplyLayout(ComputeLayout(_platformProvider?.Current.Screens));

        if (_platformProvider is not null)
        {
            _platformProvider.PlatformsChanged += OnPlatformsChanged;
        }
    }

    public Canvas Canvas { get; }

    public OverlayLayout Layout { get; private set; }

    public (double cursorX, double cursorY, bool isMouseDown) GetInput() => _mouseInput.GetInput();

    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _window.Show();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_platformProvider is not null)
        {
            _platformProvider.PlatformsChanged -= OnPlatformsChanged;
        }

        _window.SourceInitialized -= OnSourceInitialized;
        if (_window.Dispatcher.CheckAccess())
        {
            _window.Close();
        }
        else
        {
            _window.Dispatcher.InvokeAsync(_window.Close);
        }
    }

    private void ConfigureWindow()
    {
        _window.WindowStyle = WindowStyle.None;
        _window.AllowsTransparency = true;
        _window.Background = Brushes.Transparent;
        _window.ResizeMode = ResizeMode.NoResize;
        _window.WindowStartupLocation = WindowStartupLocation.Manual;
        _window.SizeToContent = SizeToContent.Manual;
        _window.Topmost = true;
        _window.ShowInTaskbar = false;
        _window.ShowActivated = false;
        _window.Content = Canvas;
        _window.SourceInitialized += OnSourceInitialized;
    }

    /// <summary>
    /// Az ablakkezelő (HWND) létrejöttekor: ne szerepeljen az Alt-Tab listában (<c>WS_EX_TOOLWINDOW</c>), és kattintásra
    /// se vegye el a fókuszt a felhasználó ablakától (<c>WS_EX_NOACTIVATE</c>).
    /// </summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(_window).Handle;
        long style = NativeMethods.GetWindowExStyle(handle);
        NativeMethods.SetWindowExStyle(handle, style | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
    }

    /// <summary>Monitor csatlakozott, levált vagy átrendeződött: az ablak kövesse. Bármelyik szálon jöhet.</summary>
    private void OnPlatformsChanged(object? sender, PlatformSnapshot snapshot)
    {
        var layout = ComputeLayout(snapshot.Screens);
        if (_window.Dispatcher.CheckAccess())
        {
            ApplyLayoutIfChanged(layout);
        }
        else
        {
            _window.Dispatcher.InvokeAsync(() => ApplyLayoutIfChanged(layout));
        }
    }

    private void ApplyLayoutIfChanged(OverlayLayout layout)
    {
        if (!_disposed && layout != Layout)
        {
            ApplyLayout(layout);
        }
    }

    private void ApplyLayout(OverlayLayout layout)
    {
        Layout = layout;
        _window.Left = layout.Bounds.Left;
        _window.Top = layout.Bounds.Top;
        _window.Width = layout.Bounds.Width;
        _window.Height = layout.Bounds.Height;
        _canvasOffset.X = -layout.Bounds.Left;
        _canvasOffset.Y = -layout.Bounds.Top;
    }

    /// <summary>
    /// A monitorlistából számolt elrendezés. Ha még nincs monitoradat (a szolgáltató nem indult el, vagy nincs
    /// szolgáltató), a WPF virtuális képernyőjét fedi le.
    /// </summary>
    private static OverlayLayout ComputeLayout(IReadOnlyList<ScreenArea>? screens) =>
        (screens is null ? null : OverlayLayout.FromScreens(screens))
        ?? new OverlayLayout(new ScreenRect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight));
}
