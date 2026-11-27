namespace CharacterEngine.Windowing;

using MonkeyStack.Core.Character;

/// <summary>
/// Az overlay ablak elhelyezése és a koordináta-eltolás. A fizika, a platformok és a kurzor képernyő-koordinátában
/// (DIP) vannak, a WPF <c>Canvas</c> viszont az ablak bal-felső sarkától számol. Ha az overlay az összes monitort lefedi,
/// a bal-felső sarka a virtuális képernyő sarka, ami a fő monitortól balra vagy fölötte lévő monitornál negatív.
/// </summary>
/// <remarks>
/// <para>
/// A méretet a platform-szolgáltató monitorlistájából (<see cref="PlatformSnapshot.Screens"/>) számoljuk, nem a WPF
/// <c>SystemParameters.VirtualScreen*</c> értékeiből, így az ablak ugyanazzal a DPI-szabállyal kerül DIP-be, mint a platformok.
/// </para>
/// <para>
/// Korlát: eltérő DPI-jű monitoroknál a szolgáltató monitoronként vált DIP-re, egyetlen WPF ablak viszont egyetlen DPI-vel
/// rajzol, ezért a két rendszer a „másik” monitoron nem fedi egymást. Azonos DPI-jű monitoroknál pontos.
/// </para>
/// </remarks>
/// <param name="Bounds">Az overlay ablak helye és mérete képernyő-koordinátában (DIP).</param>
public readonly record struct OverlayLayout(ScreenRect Bounds)
{
    /// <summary>
    /// Az összes monitort lefedő elrendezés (a monitorok teljes területének befoglaló téglalapja, a tálcát is beleértve).
    /// </summary>
    /// <returns><c>null</c>, ha még nincs monitoradat (a platform-szolgáltató nem indult el).</returns>
    public static OverlayLayout? FromScreens(IReadOnlyList<ScreenArea> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);

        if (screens.Count == 0)
        {
            return null;
        }

        double left = double.MaxValue;
        double top = double.MaxValue;
        double right = double.MinValue;
        double bottom = double.MinValue;
        foreach (var screen in screens)
        {
            left = Math.Min(left, screen.Bounds.Left);
            top = Math.Min(top, screen.Bounds.Top);
            right = Math.Max(right, screen.Bounds.Right);
            bottom = Math.Max(bottom, screen.Bounds.Bottom);
        }

        return new OverlayLayout(new ScreenRect(left, top, right, bottom));
    }

    /// <summary>Képernyő-koordináta → a Canvas koordinátája.</summary>
    public (double X, double Y) ToCanvas(double screenX, double screenY) => (screenX - Bounds.Left, screenY - Bounds.Top);

    /// <summary>A Canvas koordinátája → képernyő-koordináta.</summary>
    public (double X, double Y) ToScreen(double canvasX, double canvasY) => (canvasX + Bounds.Left, canvasY + Bounds.Top);
}
