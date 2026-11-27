namespace CharacterEngine.Windowing;

/// <summary>
/// A globális egérbemenet a fizika számára: a kurzor helye WPF egységben (DIP) és az elsődleges egérgomb állapota.
/// A <see cref="GetInput"/> pontosan azt a típust adja, amelyet a <c>RenderManager</c> bemenet-szolgáltatóként vár, így
/// metóduscsoportként átadható neki.
/// </summary>
/// <remarks>
/// <para>
/// Azért globális (Win32) lekérdezés, és nem WPF egéresemény, mert az overlay ablak a karakteren kívül áteresztő, így a
/// legtöbb egéreseményt meg sem kapja.
/// </para>
/// <para>
/// A DIP-re váltás ugyanazt a szabályt követi, mint a platform-szolgáltató (docs/os_event_engine/platform_provider_proposal.md,
/// 3.5 DPI), hogy a kurzor és a platformok ugyanabban a koordinátarendszerben legyenek: DPI-tudatlan folyamatnál a
/// koordináta már DIP, rendszerszintű tudatosságnál a rendszer DPI-jével, monitoronkéntinél (PerMonitorV2) a kurzor
/// alatti monitor DPI-jével osztunk. Így a végleges DPI-manifesttől függetlenül helyes.
/// </para>
/// <para>Szálbiztos: a fizika a léptető szálán kérdezi le, de más szálról is hívható.</para>
/// </remarks>
public sealed class GlobalMouseInput
{
    private const double BaseDpi = 96.0;

    private readonly INativeInputSource _source;
    private readonly object _syncRoot = new();
    private double _lastX;
    private double _lastY;

    /// <param name="source">A nyers lekérdezések forrása; alapértelmezetten a Win32 megvalósítás.</param>
    public GlobalMouseInput(INativeInputSource? source = null)
    {
        _source = source ?? new Win32InputSource();
    }

    /// <summary>
    /// A kurzor helye DIP-ben és az elsődleges egérgomb állapota. Ha a kurzor helye éppen nem kérdezhető le
    /// (pl. zárolt asztal), a legutóbbi ismert helyet adja.
    /// </summary>
    public (double cursorX, double cursorY, bool isMouseDown) GetInput()
    {
        var (x, y) = GetCursorPosition();
        return (x, y, IsPrimaryButtonDown());
    }

    private (double X, double Y) GetCursorPosition()
    {
        lock (_syncRoot)
        {
            if (_source.TryGetCursorPosition(out int rawX, out int rawY))
            {
                double scale = GetScale(rawX, rawY);
                _lastX = rawX / scale;
                _lastY = rawY / scale;
            }

            return (_lastX, _lastY);
        }
    }

    private double GetScale(int rawX, int rawY) => _source.GetDpiAwareness() switch
    {
        DpiAwareness.PerMonitorAware => _source.GetMonitorDpi(rawX, rawY) is uint dpi && dpi > 0 ? dpi / BaseDpi : 1.0,
        DpiAwareness.SystemAware => _source.GetSystemDpi() is uint dpi && dpi > 0 ? dpi / BaseDpi : 1.0,
        _ => 1.0,
    };

    /// <summary>
    /// Az elsődleges (a karakter megragadására használt) gomb: alapból a bal, felcserélt gombok (balkezes beállítás)
    /// esetén a jobb.
    /// </summary>
    private bool IsPrimaryButtonDown() =>
        _source.AreMouseButtonsSwapped() ? _source.IsRightButtonDown() : _source.IsLeftButtonDown();
}
