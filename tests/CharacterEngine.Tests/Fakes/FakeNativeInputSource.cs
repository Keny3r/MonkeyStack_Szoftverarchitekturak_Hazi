namespace CharacterEngine.Tests.Fakes;

using CharacterEngine.Windowing;

/// <summary>
/// Beállítható operációs rendszer-válaszok a <see cref="GlobalMouseInput"/> tesztjeihez (natív hívás nélkül).
/// </summary>
public class FakeNativeInputSource : INativeInputSource
{
    public (int X, int Y)? CursorPosition { get; set; } = (0, 0);

    public bool LeftButtonDown { get; set; }

    public bool RightButtonDown { get; set; }

    public bool ButtonsSwapped { get; set; }

    public DpiAwareness Awareness { get; set; } = DpiAwareness.Unaware;

    public uint SystemDpi { get; set; } = 96;

    /// <summary>A monitor DPI-je a kurzor X koordinátája alapján (pl. két monitor eltérő DPI-vel); <c>null</c>: hiba.</summary>
    public Func<int, uint?> MonitorDpiAtX { get; set; } = _ => 96;

    public bool TryGetCursorPosition(out int x, out int y)
    {
        (x, y) = CursorPosition ?? (0, 0);
        return CursorPosition is not null;
    }

    public bool IsLeftButtonDown() => LeftButtonDown;

    public bool IsRightButtonDown() => RightButtonDown;

    public bool AreMouseButtonsSwapped() => ButtonsSwapped;

    public DpiAwareness GetDpiAwareness() => Awareness;

    public uint GetSystemDpi() => SystemDpi;

    public uint? GetMonitorDpi(int x, int y) => MonitorDpiAtX(x);
}
